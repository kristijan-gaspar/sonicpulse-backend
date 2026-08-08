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
    TimeProvider timeProvider,
    ILogger<DetectionProcessingWorker> logger)
    : BackgroundService
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RetryDelay =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan WorkerRetryDelay =
    TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(
    CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverPendingAsync(stoppingToken);

                await foreach (var detectionId in queue.DequeueAllAsync(stoppingToken))
                {
                    await ProcessOneAsync(detectionId, stoppingToken);
                }

                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Detection processing worker failed. Retrying in {RetryDelay}",
                    WorkerRetryDelay);
            }

            if (!await WaitBeforeRetryAsync(stoppingToken))
                break;
        }
    }

    private async Task<bool> WaitBeforeRetryAsync(
    CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                WorkerRetryDelay,
                timeProvider,
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task ProcessOneAsync(
        Guid detectionId,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await ProcessAttemptAsync(detectionId, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;

                if (attempt < MaxAttempts)
                {
                    logger.LogWarning(
                        exception,
                        "Processing attempt {Attempt}/{MaxAttempts} failed for detection {DetectionId}",
                        attempt,
                        MaxAttempts,
                        detectionId);

                    await Task.Delay(
                        RetryDelay,
                        timeProvider,
                        cancellationToken);
                }
            }
        }

        logger.LogError(
            lastException,
            "Detection {DetectionId} permanently failed after {MaxAttempts} attempts",
            detectionId,
            MaxAttempts);

        await TryMarkFailedAsync(detectionId, cancellationToken);
    }

    private async Task ProcessAttemptAsync(
        Guid detectionId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ProcessDetectionHandler>();

        await handler.HandleAsync(detectionId, cancellationToken);
    }

    private async Task TryMarkFailedAsync(
        Guid detectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var detections = scope.ServiceProvider
                .GetRequiredService<IDetectionRepository>();

            var unitOfWork = scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

            var detection = await detections.GetForProcessingAsync(
                detectionId,
                cancellationToken);

            if (detection is null ||
                detection.ProcessingStatus != DetectionProcessingStatus.Pending)
            {
                return;
            }

            detection.MarkFailed();

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not mark detection {DetectionId} as failed",
                detectionId);
        }
    }

    private async Task RecoverPendingAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var detections = scope.ServiceProvider
            .GetRequiredService<IDetectionRepository>();

        var pendingIds =
            await detections.GetPendingIdsAsync(cancellationToken);

        foreach (var detectionId in pendingIds)
        {
            await queue.EnqueueAsync(detectionId, cancellationToken);
        }

        logger.LogInformation(
            "Recovered {Count} pending detections after startup",
            pendingIds.Count);
    }
}