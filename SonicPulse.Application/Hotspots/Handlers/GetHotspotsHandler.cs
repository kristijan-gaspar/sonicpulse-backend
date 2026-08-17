using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Hotspots.Dtos;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Application.Hotspots.Handlers;

public sealed class GetHotspotsHandler(IHotspotRepository hotspots, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<HotspotDto>> HandleAsync(int sinceHours, CancellationToken ct)
    {
        var clampedHours = Math.Clamp(sinceHours, 1, 168);
        var sinceUtc = timeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromHours(clampedHours);

        var result = await hotspots.GetSinceAsync(sinceUtc, ct);
        return result.Select(ToDto).ToList();
    }

    internal static HotspotDto ToDto(Hotspot hotspot) => new(
        hotspot.Id, hotspot.Centroid.Latitude, hotspot.Centroid.Longitude, hotspot.RadiusMeters,
        hotspot.DeviceCount, hotspot.FirstReceivedAtUtc, hotspot.LastReceivedAtUtc);
}
