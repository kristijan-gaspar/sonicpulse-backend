using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Enums;

namespace SonicPulse.Infrastructure.Processing;

public sealed class DetectionProcessingWorker(
    IDetectionProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DetectionProcessingWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);

        await foreach (var detectionId in queue.DequeueAllAsync(stoppingToken))
            await ProcessOneAsync(detectionId, stoppingToken);
    }

    private async Task ProcessOneAsync(Guid detectionId, CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                // Fresh scope/DbContext per attempt: a failed attempt must not
                // leave partially-tracked changes for the retry to build on.
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ProcessDetectionHandler>();
                await handler.HandleAsync(detectionId, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Application shutdown, not a processing failure. Leave the
                // detection Pending - startup recovery picks it up next launch.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for detection {DetectionId}",
                    attempt, MaxAttempts, detectionId);

                if (attempt == MaxAttempts)
                {
                    logger.LogError(
                        "Detection {DetectionId} permanently failed after {MaxAttempts} attempts",
                        detectionId, MaxAttempts);
                    await TryMarkFailedAsync(detectionId, stoppingToken);
                    return;
                }

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
    }

    private async Task TryMarkFailedAsync(Guid detectionId, CancellationToken stoppingToken)
    {
        try
        {
            // Fresh scope/DbContext: avoids persisting any partially-tracked
            // changes left over from the failed attempt.
            using var scope = scopeFactory.CreateScope();
            var detections = scope.ServiceProvider.GetRequiredService<IDetectionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var detection = await detections.GetForProcessingAsync(detectionId, stoppingToken);
            if (detection is null || detection.ProcessingStatus != DetectionProcessingStatus.Pending)
                return; // already resolved by some other path; nothing to do

            detection.MarkFailed();
            await unitOfWork.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Could not even persist the Failed status (e.g. DB briefly
            // unreachable). Catching here (instead of letting it propagate)
            // is what keeps one transient failure from taking down the
            // entire worker loop, and with it every other queued item.
            logger.LogError(ex, "Could not mark detection {DetectionId} as failed", detectionId);
        }
    }

    private async Task RecoverPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var detections = scope.ServiceProvider.GetRequiredService<IDetectionRepository>();

        var pendingIds = await detections.GetPendingIdsAsync(ct);
        foreach (var id in pendingIds)
            await queue.EnqueueAsync(id, ct);

        logger.LogInformation("Recovered {Count} pending detections after startup", pendingIds.Count);
    }
}
