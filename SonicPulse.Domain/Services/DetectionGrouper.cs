using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;

namespace SonicPulse.Domain.Services;

public static class DetectionGrouper
{
    public static IReadOnlyList<Detection> SelectGroup(
        Detection incoming, IReadOnlyList<Detection> candidates, GroupingRules rules)
    {
        var neighbors = candidates.Where(c =>
            c.Id != incoming.Id &&
            IsWithinTimeWindow(incoming, c, rules) &&
            IsWithinRadius(incoming, c, rules));

        var group = neighbors.Append(incoming).ToList();

        var distinctDevices = group.Select(d => d.DeviceId).Distinct().Count();

        return distinctDevices >= rules.MinDeviceCount
            ? group
            : Array.Empty<Detection>();
    }

    private static bool IsWithinTimeWindow(Detection a, Detection b, GroupingRules rules)
        => (a.ReceivedAtUtc - b.ReceivedAtUtc).Duration() <= rules.TimeWindow;

    private static bool IsWithinRadius(Detection a, Detection b, GroupingRules rules)
        => GeoDistance.Meters(a.Location, b.Location) <= rules.RadiusMeters;
}
