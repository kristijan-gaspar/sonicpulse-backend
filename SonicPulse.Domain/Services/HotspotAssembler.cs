using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Services;

public static class HotspotAssembler
{
    public static Hotspot Assemble(IReadOnlyList<Detection> group, GroupingRules rules)
    {
        // Fresh hotspot: the whole group IS the newly-matched batch.
        var computed = Compute(group, group, rules);
        return Hotspot.Create(
            computed.Centroid, computed.RadiusMeters, computed.Confidence,
            computed.DeviceCount, computed.First, computed.Last);
    }

    /// <param name="allMembers">The hotspot's full membership (existing + newly matched) -
    /// drives centroid, radius, and device count.</param>
    /// <param name="recentBatch">Only this processing pass's newly-matched detections -
    /// drives confidence's time-compactness term, see HeuristicConfidenceScorer.</param>
    public static void Reassemble(
        Hotspot existing, IReadOnlyList<Detection> allMembers,
        IReadOnlyList<Detection> recentBatch, GroupingRules rules)
    {
        var computed = Compute(allMembers, recentBatch, rules);
        existing.Recalculate(
            computed.Centroid, computed.RadiusMeters, computed.Confidence,
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
        Coordinates Centroid, double RadiusMeters, int Confidence,
        int DeviceCount, DateTime First, DateTime Last) Compute(
        IReadOnlyList<Detection> allMembers, IReadOnlyList<Detection> recentBatch, GroupingRules rules)
    {
        var centroid = WeightedCentroidLocationEstimator.Estimate(allMembers);

        double maxDistance = allMembers.Max(d => GeoDistance.Meters(centroid, d.Location));
        double radius = 0.5 * maxDistance;

        int confidence = HeuristicConfidenceScorer.Score(allMembers, recentBatch, rules);
        int deviceCount = allMembers.Select(d => d.DeviceId).Distinct().Count();

        return (centroid, radius, confidence, deviceCount,
                allMembers.Min(d => d.ReceivedAtUtc), allMembers.Max(d => d.ReceivedAtUtc));
    }
}
