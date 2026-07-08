using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Application.Detections.Handlers;

public sealed class SubmitDetectionHandler(
    IDetectionRepository detections,
    TimeProvider timeProvider)
{
    public async Task<SubmitDetectionResponse> HandleAsync(
        SubmitDetectionRequest request, CancellationToken ct)
    {
        var receivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        var detection = Detection.Create(
            DeviceId.From(request.DeviceId),
            request.PeakDbfs,
            new Coordinates(request.Latitude, request.Longitude),
            request.GpsAccuracy,
            receivedAtUtc,
            request.PeakTimeClient);

        await detections.AddAsync(detection, ct);

        return new SubmitDetectionResponse(detection.Id);
    }
}
