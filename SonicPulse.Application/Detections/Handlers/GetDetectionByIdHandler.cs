using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Application.Detections.Handlers;

public sealed class GetDetectionByIdHandler(IDetectionRepository detections)
{
    public async Task<DetectionDto?> HandleAsync(Guid id, CancellationToken ct)
    {
        var detection = await detections.GetByIdAsync(id, ct);
        return detection is null ? null : ToDto(detection);
    }

    internal static DetectionDto ToDto(Detection detection) => new(
        detection.Id, detection.SequenceNumber, detection.DeviceId.Value, detection.PeakDbfs,
        detection.Location.Latitude, detection.Location.Longitude, detection.GpsAccuracy,
        detection.ReceivedAtUtc, detection.PeakTimeClient);
}
