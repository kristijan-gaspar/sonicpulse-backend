using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Application.Detections.Handlers;

public sealed class GetDetectionsByDeviceHandler(IDetectionRepository detections)
{
    public async Task<PagedResult<DetectionDto>> HandleAsync(
        Guid deviceId, long? cursor, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100); 

        var rows = await detections.GetByDeviceIdAsync(
            DeviceId.From(deviceId), cursor, limit, ct);

        bool hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        return new PagedResult<DetectionDto>(
            page.Select(GetDetectionByIdHandler.ToDto).ToList(),
            hasMore ? page[^1].SequenceNumber : null);
    }
}
