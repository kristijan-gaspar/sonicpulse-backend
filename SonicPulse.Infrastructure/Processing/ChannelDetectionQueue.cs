using System.Threading.Channels;
using SonicPulse.Application.Abstractions;

namespace SonicPulse.Infrastructure.Processing;

public sealed class ChannelDetectionQueue : IDetectionProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid detectionId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(detectionId, ct);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
