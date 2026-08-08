using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SonicPulse.Domain.Rules;
using SonicPulse.Infrastructure.Retention;

namespace SonicPulse.Tests.Infrastructure;

public class DataRetentionWorkerTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static IOptions<DataRetentionOptions> ValidOptions() =>
        Options.Create(new DataRetentionOptions
        {
            RetentionHours = 168,
            CleanupIntervalHours = 6
        });

    private static DataRetentionWorker CreateWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions>? options = null,
        TimeProvider? timeProvider = null)
    {
        return new DataRetentionWorker(
            scopeFactory,
            options ?? ValidOptions(),
            Rules,
            timeProvider ?? TimeProvider.System,
            NullLogger<DataRetentionWorker>.Instance);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(condition());
    }

    [Fact]
    public void InvalidRetentionHours_Throws()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var options = Options.Create(new DataRetentionOptions
        {
            RetentionHours = 0,
            CleanupIntervalHours = 6
        });

        Assert.Throws<InvalidOperationException>(() =>
            CreateWorker(scopeFactory.Object, options));
    }

    [Fact]
    public void InvalidCleanupIntervalHours_Throws()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var options = Options.Create(new DataRetentionOptions
        {
            RetentionHours = 168,
            CleanupIntervalHours = 0
        });

        Assert.Throws<InvalidOperationException>(() =>
            CreateWorker(scopeFactory.Object, options));
    }

    [Fact]
    public async Task CleanupFailure_RetriesAfterDelay()
    {
        var scopeCalls = 0;

        var scopeFactory = new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(factory => factory.CreateScope())
            .Returns(() =>
            {
                Interlocked.Increment(ref scopeCalls);

                throw new InvalidOperationException(
                    "Simulated database failure");
            });

        var timeProvider =
            new FakeTimeProvider(DateTimeOffset.UtcNow);

        var worker = CreateWorker(
            scopeFactory.Object,
            timeProvider: timeProvider);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntilAsync(
            () => Volatile.Read(ref scopeCalls) >= 1,
            TimeSpan.FromSeconds(1));

        timeProvider.Advance(TimeSpan.FromSeconds(30));

        await WaitUntilAsync(
            () => Volatile.Read(ref scopeCalls) >= 2,
            TimeSpan.FromSeconds(1));

        await worker.StopAsync(CancellationToken.None);

        Assert.True(scopeCalls >= 2);
    }

    [Fact]
    public async Task ShutdownDuringRetryDelay_StopsCleanly()
    {
        var scopeCalls = 0;

        var scopeFactory = new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(factory => factory.CreateScope())
            .Returns(() =>
            {
                Interlocked.Increment(ref scopeCalls);

                throw new InvalidOperationException(
                    "Simulated database failure");
            });

        var timeProvider =
            new FakeTimeProvider(DateTimeOffset.UtcNow);

        var worker = CreateWorker(
            scopeFactory.Object,
            timeProvider: timeProvider);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntilAsync(
            () => Volatile.Read(ref scopeCalls) == 1,
            TimeSpan.FromSeconds(1));

        await worker
            .StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, scopeCalls);
    }
}