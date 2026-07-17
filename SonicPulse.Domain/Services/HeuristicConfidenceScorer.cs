using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;

namespace SonicPulse.Domain.Services;

public static class HeuristicConfidenceScorer
{
    /// <param name="allMembers">The hotspot's full membership - drives deviceFactor.</param>
    /// <param name="recentBatch">Only this processing pass's newly-matched detections -
    /// drives timeCompactnessFactor, so an old hotspot's accumulated history doesn't
    /// permanently floor the score even when a fresh, tightly-clustered burst arrives.</param>
    public static int Score(
        IReadOnlyList<Detection> allMembers, IReadOnlyList<Detection> recentBatch, GroupingRules rules)
    {
        int deviceCount = allMembers.Select(d => d.DeviceId).Distinct().Count();
        int deviceFactor = Math.Min(50, 20 + 10 * (deviceCount - 2));

        double timeSpanSeconds =
            (recentBatch.Max(d => d.ReceivedAtUtc) - recentBatch.Min(d => d.ReceivedAtUtc))
            .TotalSeconds;

        double compactness = 50.0 * (1.0 - timeSpanSeconds / rules.TimeWindow.TotalSeconds);
        int timeCompactnessFactor = (int)Math.Clamp(compactness, 0, 50);

        return deviceFactor + timeCompactnessFactor;
    }
}
