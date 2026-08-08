using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SonicPulse.Domain.Enums;
using SonicPulse.Domain.Rules;
using SonicPulse.Infrastructure.Persistence;

namespace SonicPulse.Infrastructure.Retention;

public sealed class DataRetentionWorker : BackgroundService
{
    private static readonly TimeSpan FailureRetryDelay =
        TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataRetentionOptions _options;
    private readonly GroupingRules _groupingRules;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DataRetentionWorker> _logger;

    public DataRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        GroupingRules groupingRules,
        TimeProvider timeProvider,
        ILogger<DataRetentionWorker> logger)
    {
        ValidateRetentionOptions(options);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _groupingRules = groupingRules;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private static void ValidateRetentionOptions(IOptions<DataRetentionOptions> options)
    {
        if (options.Value.RetentionHours <= 0)
            throw new InvalidOperationException(
                "RetentionHours must be greater than 0.");

        if (options.Value.CleanupIntervalHours <= 0)
            throw new InvalidOperationException(
                "CleanupIntervalHours must be greater than 0.");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);

                await WaitForNextCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Data retention cleanup failed. Retrying in {RetryDelay}",
                    FailureRetryDelay);

                if (!await WaitBeforeRetryAsync(stoppingToken))
                    break;
            }
        }
    }

    private async Task CleanupAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var cutoff = GetCutoff();

        if (await HasBlockingPendingDetectionAsync(
                db,
                cutoff,
                cancellationToken))
        {
            _logger.LogWarning(
                "Data retention cleanup skipped because old pending detections still require processing.");

            return;
        }

        await DeleteExpiredDataAsync(
            db,
            cutoff,
            cancellationToken);
    }

    private DateTime GetCutoff()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime
            .AddHours(-_options.RetentionHours);
    }

    private async Task<bool> HasBlockingPendingDetectionAsync(
        AppDbContext db,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var pendingSafetyCutoff =
            cutoff.Add(_groupingRules.TimeWindow);

        return await db.Detections
            .AsNoTracking()
            .AnyAsync(
                detection =>
                    detection.ProcessingStatus ==
                        DetectionProcessingStatus.Pending &&
                    detection.ReceivedAtUtc <= pendingSafetyCutoff,
                cancellationToken);
    }

    private async Task DeleteExpiredDataAsync(
        AppDbContext db,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                cancellationToken);

        var deletedDetections =
            await DeleteExpiredStandaloneDetectionsAsync(
                db,
                cutoff,
                cancellationToken);

        var deletedHotspots =
            await DeleteExpiredHotspotsAsync(
                db,
                cutoff,
                cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        _logger.LogInformation(
            "Data retention cleanup completed. Deleted {DetectionCount} standalone detections and {HotspotCount} hotspots.",
            deletedDetections,
            deletedHotspots);
    }

    private static async Task<int> DeleteExpiredStandaloneDetectionsAsync(
        AppDbContext db,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        return await db.Detections
            .Where(detection =>
                detection.HotspotId == null &&
                detection.ProcessingStatus !=
                    DetectionProcessingStatus.Pending &&
                detection.ReceivedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<int> DeleteExpiredHotspotsAsync(
        AppDbContext db,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        return await db.Hotspots
            .Where(hotspot =>
                hotspot.LastReceivedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task WaitForNextCleanupAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromHours(_options.CleanupIntervalHours),
            _timeProvider,
            cancellationToken);
    }

    private async Task<bool> WaitBeforeRetryAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                FailureRetryDelay,
                _timeProvider,
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}