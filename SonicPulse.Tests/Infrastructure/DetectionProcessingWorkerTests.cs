using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Enums;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.ValueObjects;
using SonicPulse.Infrastructure.Processing;

namespace SonicPulse.Tests.Infrastructure;

public class DetectionProcessingWorkerTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static Detection MakeDetection() =>
        Detection.Create(DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);

    // Minimal, non-Moq stand-ins for IServiceScope/IServiceProvider. The worker
    // calls scopeFactory.CreateScope() once per attempt and expects a fresh
    // scope each time - a plain class is easier to read here than a mocked one.
    private sealed class FakeScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() { }
    }

    private sealed class FakeProvider(params (Type Type, object Instance)[] services) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            services.FirstOrDefault(s => s.Type == serviceType).Instance;
    }

    // Everything one processing attempt needs: its own IDetectionRepository/
    // IHotspotRepository/IUnitOfWork mocks and the real ProcessDetectionHandler
    // wired to them. Building a fresh set per attempt is what proves attempts
    // don't share tracked entities or a DbContext.
    private sealed record Attempt(
        Mock<IDetectionRepository> Detections, Mock<IUnitOfWork> UnitOfWork, IServiceScope Scope);

    private static Attempt BuildAttempt(Detection detection, bool shouldFail)
    {
        var detections = new Mock<IDetectionRepository>();
        detections
            .Setup(r => r.GetForProcessingAsync(detection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (shouldFail)
                    throw new InvalidOperationException("Simulated transient failure");
                return detection;
            });
        detections
            .Setup(r => r.FindCandidatesAsync(
                It.IsAny<Coordinates>(), It.IsAny<DateTime>(), It.IsAny<GroupingRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Detection>)[]); // no candidates -> lone detection, MarkProcessed

        var hotspots = new Mock<IHotspotRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ProcessDetectionHandler(detections.Object, hotspots.Object, unitOfWork.Object, Rules);
        var provider = new FakeProvider(
            (typeof(ProcessDetectionHandler), handler),
            (typeof(IDetectionRepository), detections.Object),
            (typeof(IUnitOfWork), unitOfWork.Object));

        return new Attempt(detections, unitOfWork, new FakeScope(provider));
    }

    private static IServiceScope BuildRecoveryScope(bool shouldFail = false)
    {
        var detections = new Mock<IDetectionRepository>();

        detections
            .Setup(r => r.GetPendingIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (shouldFail)
                    throw new InvalidOperationException(
                        "Simulated recovery failure");

                return [];
            });

        var provider = new FakeProvider(
            (typeof(IDetectionRepository), detections.Object));

        return new FakeScope(provider);
    }

    // scopeFactory.CreateScope() is called once for startup recovery, then
    // once per processing attempt (plus one more if every attempt fails, for
    // TryMarkFailedAsync's own lookup). `attemptResults` says, per call after
    // the first, whether that attempt should throw (true) or succeed (false).
    private static (Mock<IServiceScopeFactory> Factory, List<Attempt> Attempts) BuildScopeFactory(
        Detection detection, IReadOnlyList<bool> attemptResults)
    {
        var attempts = new List<Attempt>();
        var callNumber = 0;

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            callNumber++;
            if (callNumber == 1)
                return BuildRecoveryScope();

            var attemptIndex = callNumber - 2; // 0-based, after the recovery call
            var shouldFail = attemptIndex < attemptResults.Count && attemptResults[attemptIndex];
            var attempt = BuildAttempt(detection, shouldFail);
            attempts.Add(attempt);
            return attempt.Scope;
        });

        return (scopeFactory, attempts);
    }

    // Yields the given ids once, then blocks (like the real queue does)
    // until the worker is stopped.
    private sealed class OneShotQueue(params Guid[] ids) : IDetectionProcessingQueue
    {
        public ValueTask EnqueueAsync(Guid detectionId, CancellationToken ct) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<Guid> DequeueAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var id in ids)
                yield return id;

            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

    // Starts the worker and polls until `isDone` is true or the timeout hits,
    // then stops it. BackgroundService doesn't expose a "wait for one item to
    // finish" hook, so polling is the simplest honest way to wait here.
    private static async Task RunUntilAsync(DetectionProcessingWorker worker, Func<bool> isDone, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow + timeout;
        while (!isDone() && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailsOnce_ThenSucceedsOnRetry()
    {
        var detection = MakeDetection();
        var (scopeFactory, attempts) = BuildScopeFactory(detection, attemptResults: [true, false]);
        var timeProvider = TimeProvider.System;

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(detection.Id),
            scopeFactory.Object,
            timeProvider,
            NullLogger<DetectionProcessingWorker>.Instance);

        await RunUntilAsync(worker, () => detection.ProcessingStatus != DetectionProcessingStatus.Pending, TimeSpan.FromSeconds(5));

        Assert.Equal(2, attempts.Count);
        Assert.Equal(DetectionProcessingStatus.Processed, detection.ProcessingStatus);
    }

    [Fact]
    public async Task FailsAllAttempts_MarksFailed()
    {
        var detection = MakeDetection();
        // All 3 HandleAsync attempts fail; TryMarkFailedAsync's own lookup
        // (attemptResults has no 4th entry, so it defaults to "succeed") then
        // persists the Failed status.
        var (scopeFactory, attempts) = BuildScopeFactory(detection, attemptResults: [true, true, true]);
        var timeProvider = TimeProvider.System;

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(detection.Id),
            scopeFactory.Object,
            timeProvider,
            NullLogger<DetectionProcessingWorker>.Instance);

        await RunUntilAsync(worker, () => detection.ProcessingStatus == DetectionProcessingStatus.Failed, TimeSpan.FromSeconds(5));

        Assert.Equal(4, attempts.Count); // 3 failed HandleAsync attempts + TryMarkFailedAsync's lookup
        Assert.Equal(DetectionProcessingStatus.Failed, detection.ProcessingStatus);
    }

    [Fact]
    public async Task EachAttempt_OnlyTheSuccessfulOneSaves()
    {
        var detection = MakeDetection();
        var (scopeFactory, attempts) = BuildScopeFactory(detection, attemptResults: [true, true, false]);
        var timeProvider = TimeProvider.System;

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(detection.Id),
            scopeFactory.Object,
            timeProvider,
            NullLogger<DetectionProcessingWorker>.Instance);
        await RunUntilAsync(worker, () => detection.ProcessingStatus != DetectionProcessingStatus.Pending, TimeSpan.FromSeconds(5));

        Assert.Equal(3, attempts.Count); // 3 distinct scopes, 3 distinct IUnitOfWork mocks
        attempts[0].UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        attempts[1].UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        attempts[2].UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShutdownDuringRetryDelay_LeavesDetectionPending()
    {
        var detection = MakeDetection();
        var (scopeFactory, _) = BuildScopeFactory(detection, attemptResults: [true, true, true]);
        var timeProvider = TimeProvider.System;

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(detection.Id),
            scopeFactory.Object,
            timeProvider,
            NullLogger<DetectionProcessingWorker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        await Task.Delay(50); // let the first attempt fail and the retry delay begin
        await cts.CancelAsync(); // simulate application shutdown mid-delay
        await worker.StopAsync(CancellationToken.None);
        await Task.Delay(200); // let the worker's background task settle

        Assert.Equal(DetectionProcessingStatus.Pending, detection.ProcessingStatus);
    }

    [Fact]
    public async Task RecoveryFailsOnce_ThenRetries()
    {
        var recoveryCalls = 0;

        var scopeFactory = new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(() =>
            {
                var call = Interlocked.Increment(ref recoveryCalls);

                return BuildRecoveryScope(
                    shouldFail: call == 1);
            });

        var timeProvider =
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(),
            scopeFactory.Object,
            timeProvider,
            NullLogger<DetectionProcessingWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (Volatile.Read(ref recoveryCalls) < 1 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        await Task.Delay(20); // give the worker a chance to schedule the retry delay
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        deadline = DateTime.UtcNow.AddSeconds(1);
        while (Volatile.Read(ref recoveryCalls) < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, recoveryCalls);
    }

    [Fact]
    public async Task ShutdownDuringWorkerRetryDelay_StopsCleanly()
    {
        var recoveryCalls = 0;

        var scopeFactory = new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(() =>
            {
                Interlocked.Increment(ref recoveryCalls);

                return BuildRecoveryScope(
                    shouldFail: true);
            });

        var worker = new DetectionProcessingWorker(
            new OneShotQueue(),
            scopeFactory.Object,
            TimeProvider.System,
            NullLogger<DetectionProcessingWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(1);

        while (Volatile.Read(ref recoveryCalls) == 0 &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        await Task.Delay(50);

        await worker
            .StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, recoveryCalls);
    }


}
