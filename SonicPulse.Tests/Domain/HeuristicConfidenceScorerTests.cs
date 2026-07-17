using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class HeuristicConfidenceScorerTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static readonly DateTime BaseTime = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
    private static readonly Coordinates Location = new(45.8010, 15.9700);

    private static List<Detection> MakeDetections(int deviceCount, double timeSpanSeconds)
        => Enumerable.Range(0, deviceCount)
            .Select(i => Detection.Create(
                DeviceId.New(), -8.5, Location, 12.0,
                BaseTime.AddSeconds(i == deviceCount - 1 ? timeSpanSeconds : 0), null))
            .ToList();

    [Theory]
    [InlineData(2, 0.0, 70)]
    [InlineData(3, 2.5, 55)]
    [InlineData(4, 5.0, 40)]
    [InlineData(7, 0.0, 100)] // deviceFactor caps at 50, total caps at 100
    public void Score_KnownInputs_MatchesExpectedConfidence(
        int deviceCount, double timeSpanSeconds, int expectedConfidence)
    {
        var detections = MakeDetections(deviceCount, timeSpanSeconds);

        var confidence = HeuristicConfidenceScorer.Score(detections, detections, Rules);

        Assert.Equal(expectedConfidence, confidence);
    }

    [Fact]
    public void Score_RecentBatchTight_IgnoresOlderHotspotHistory()
    {
        // allMembers spans 10s (an old, long-lived hotspot), but this pass's
        // recentBatch all arrived at the same instant - confidence should
        // reflect that tight recent burst, not the full historical span.
        var old = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime, null);
        var newA = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime.AddSeconds(10), null);
        var newB = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime.AddSeconds(10), null);

        var allMembers = new List<Detection> { old, newA, newB };
        var recentBatch = new List<Detection> { newA, newB };

        var confidence = HeuristicConfidenceScorer.Score(allMembers, recentBatch, Rules);

        // deviceFactor: 3 devices -> min(50, 20+10*1) = 30
        // timeCompactnessFactor: recentBatch span 0s -> 50 (not floored by the 10s full history)
        Assert.Equal(80, confidence);
    }
}
