using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;

namespace SonicPulse.Domain.Services;

public static class HeuristicConfidenceScorer
{
    public static int Score(IReadOnlyList<Detection> detections, GroupingRules rules)
    {
        int deviceCount = detections.Select(d => d.DeviceId).Distinct().Count();
        int deviceFactor = Math.Min(50, 20 + 10 * (deviceCount - 2));

        double timeSpanSeconds =
            (detections.Max(d => d.ReceivedAtUtc) - detections.Min(d => d.ReceivedAtUtc))
            .TotalSeconds;

        double compactness = 50.0 * (1.0 - timeSpanSeconds / rules.TimeWindow.TotalSeconds);
        int timeCompactnessFactor = (int)Math.Clamp(compactness, 0, 50);

        return deviceFactor + timeCompactnessFactor;
    }
}
