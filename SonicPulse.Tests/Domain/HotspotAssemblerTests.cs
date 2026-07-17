using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class HotspotAssemblerTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static readonly DateTime BaseTime = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);

    private static Detection MakeDetection(
        Coordinates location, DateTime receivedAtUtc, DeviceId? deviceId = null)
        => Detection.Create(deviceId ?? DeviceId.New(), -8.5, location, 12.0, receivedAtUtc, null);

    [Fact]
    public void Assemble_RadiusEqualsHalfMaxDistanceFromCentroid()
    {
        var group = new List<Detection>
        {
            MakeDetection(new Coordinates(45.8010, 15.9700), BaseTime),
            MakeDetection(new Coordinates(45.8100, 15.9800), BaseTime.AddSeconds(1))
        };

        var hotspot = HotspotAssembler.Assemble(group, Rules);

        var centroid = WeightedCentroidLocationEstimator.Estimate(group);
        var expectedRadius = 0.5 * group.Max(d => GeoDistance.Meters(centroid, d.Location));

        Assert.Equal(expectedRadius, hotspot.RadiusMeters, precision: 6);
    }

    [Fact]
    public void Assemble_SetsDeviceCountAndTimeRange()
    {
        var group = new List<Detection>
        {
            MakeDetection(new Coordinates(45.8010, 15.9700), BaseTime),
            MakeDetection(new Coordinates(45.8020, 15.9710), BaseTime.AddSeconds(2))
        };

        var hotspot = HotspotAssembler.Assemble(group, Rules);

        Assert.Equal(2, hotspot.DeviceCount);
        Assert.Equal(BaseTime, hotspot.FirstReceivedAtUtc);
        Assert.Equal(BaseTime.AddSeconds(2), hotspot.LastReceivedAtUtc);
    }

    [Fact]
    public void Reassemble_MutatesExistingObject()
    {
        var initialGroup = new List<Detection>
        {
            MakeDetection(new Coordinates(45.8010, 15.9700), BaseTime),
            MakeDetection(new Coordinates(45.8020, 15.9710), BaseTime.AddSeconds(1))
        };
        var hotspot = HotspotAssembler.Assemble(initialGroup, Rules);
        var id = hotspot.Id;

        var expandedGroup = new List<Detection>(initialGroup)
        {
            MakeDetection(new Coordinates(45.8030, 15.9720), BaseTime.AddSeconds(2))
        };

        HotspotAssembler.Reassemble(hotspot, expandedGroup, expandedGroup, Rules);

        Assert.Equal(id, hotspot.Id);
        Assert.Equal(3, hotspot.DeviceCount);
        Assert.Equal(BaseTime.AddSeconds(2), hotspot.LastReceivedAtUtc);
    }

    [Fact]
    public void ResolveBridging_PicksHotspotWithMostMembers()
    {
        var hotspotA = Guid.NewGuid();
        var hotspotB = Guid.NewGuid();

        var group = new List<Detection>
        {
            MakeDetection(new Coordinates(45.8010, 15.9700), BaseTime),
            MakeDetection(new Coordinates(45.8011, 15.9701), BaseTime.AddSeconds(1)),
            MakeDetection(new Coordinates(45.8012, 15.9702), BaseTime.AddSeconds(2))
        };
        group[0].AssignToHotspot(hotspotA);
        group[1].AssignToHotspot(hotspotA);
        group[2].AssignToHotspot(hotspotB);

        var (chosen, filtered) = HotspotAssembler.ResolveBridging(group);

        Assert.Equal(hotspotA, chosen);
        Assert.Equal(2, filtered.Count);
        Assert.DoesNotContain(group[2], filtered);
    }

    [Fact]
    public void ResolveBridging_TieBrokenByMostRecentReceivedAtUtc()
    {
        var hotspotA = Guid.NewGuid();
        var hotspotB = Guid.NewGuid();

        var older = MakeDetection(new Coordinates(45.8010, 15.9700), BaseTime);
        var newer = MakeDetection(new Coordinates(45.8011, 15.9701), BaseTime.AddSeconds(3));
        older.AssignToHotspot(hotspotA);
        newer.AssignToHotspot(hotspotB);

        var (chosen, _) = HotspotAssembler.ResolveBridging([older, newer]);

        Assert.Equal(hotspotB, chosen);
    }
}
