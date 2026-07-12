namespace SonicPulse.Application.Abstractions;

public interface IDetectionProcessingQueue
{
    ValueTask EnqueueAsync(Guid detectionId, CancellationToken ct);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}
