using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Services;

public static class HotspotAssembler
{
    public static Hotspot Assemble(IReadOnlyList<Detection> group)
    {
        var computed = Compute(group);
        return Hotspot.Create(
            computed.Centroid, computed.RadiusMeters,
            computed.DeviceCount, computed.First, computed.Last);
    }

    public static void Reassemble(Hotspot existing, IReadOnlyList<Detection> group)
    {
        var computed = Compute(group);
        existing.Recalculate(
            computed.Centroid, computed.RadiusMeters,
            computed.DeviceCount, computed.First, computed.Last);
    }

    public static (Guid ChosenHotspotId, IReadOnlyList<Detection> FilteredGroup) ResolveBridging(
        IReadOnlyList<Detection> group)
    {
        var chosen = group
            .Where(d => d.HotspotId is not null)
            .GroupBy(d => d.HotspotId!.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(d => d.ReceivedAtUtc))
            .ThenBy(g => g.Key) // final tie-break: HotspotId, guarantees a deterministic winner
            .First().Key;

        var filtered = group
            .Where(d => d.HotspotId is null || d.HotspotId == chosen)
            .ToList();

        return (chosen, filtered);
    }

    private static (
        Coordinates Centroid, double RadiusMeters,
        int DeviceCount, DateTime First, DateTime Last) Compute(
        IReadOnlyList<Detection> group)
    {
        // One device, one spatial vote: a device that sends several detections
        // for the same event must not out-weigh a device that sent one, so
        // centroid/radius use one representative per DeviceId (best GPS
        // accuracy, then earliest, then Id - fully deterministic).
        // deviceCount and timestamps still use the full group.
        var spatialRepresentatives = group
            .GroupBy(d => d.DeviceId)
            .Select(g => g
                .OrderBy(d => d.GpsAccuracy)
                .ThenBy(d => d.ReceivedAtUtc)
                .ThenBy(d => d.Id)
                .First())
            .ToList();

        var centroid = WeightedCentroidLocationEstimator.Estimate(spatialRepresentatives);

        double radius = spatialRepresentatives.Max(d => GeoDistance.Meters(centroid, d.Location));

        int deviceCount = group.Select(d => d.DeviceId).Distinct().Count();

        return (centroid, radius, deviceCount,
                group.Min(d => d.ReceivedAtUtc), group.Max(d => d.ReceivedAtUtc));
    }
}
