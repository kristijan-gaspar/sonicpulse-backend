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

        var confidence = HeuristicConfidenceScorer.Score(detections, Rules);

        Assert.Equal(expectedConfidence, confidence);
    }

    [Fact]
    public void Score_IsDeterministic_RegardlessOfMemberOrder()
    {
        var a = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime, null);
        var b = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime.AddSeconds(2), null);
        var c = Detection.Create(DeviceId.New(), -8.5, Location, 12.0, BaseTime.AddSeconds(4), null);

        var forward = HeuristicConfidenceScorer.Score([a, b, c], Rules);
        var reversed = HeuristicConfidenceScorer.Score([c, b, a], Rules);
        var shuffled = HeuristicConfidenceScorer.Score([b, a, c], Rules);

        Assert.Equal(forward, reversed);
        Assert.Equal(forward, shuffled);
    }
}
