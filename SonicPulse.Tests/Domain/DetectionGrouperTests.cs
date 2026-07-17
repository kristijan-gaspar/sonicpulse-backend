using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class DetectionGrouperTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static readonly DateTime BaseTime = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
    private static readonly Coordinates BaseLocation = new(45.8010, 15.9700);

    private static Detection MakeDetection(
        DeviceId deviceId, Coordinates location, DateTime receivedAtUtc)
        => Detection.Create(deviceId, -8.5, location, 12.0, receivedAtUtc, null);

    [Fact]
    public void SelectGroup_CandidateOutsideTimeWindow_IsExcluded()
    {
        var incoming = MakeDetection(DeviceId.New(), BaseLocation, BaseTime);
        var tooLate = MakeDetection(DeviceId.New(), BaseLocation, BaseTime.AddSeconds(5.1));

        var group = DetectionGrouper.SelectGroup(incoming, [tooLate], Rules);

        Assert.Empty(group);
    }

    [Fact]
    public void SelectGroup_CandidateOutsideRadius_IsExcluded()
    {
        var incoming = MakeDetection(DeviceId.New(), BaseLocation, BaseTime);
        var tooFar = MakeDetection(
            DeviceId.New(), new Coordinates(BaseLocation.Latitude + 1, BaseLocation.Longitude), BaseTime);

        var group = DetectionGrouper.SelectGroup(incoming, [tooFar], Rules);

        Assert.Empty(group);
    }

    [Fact]
    public void SelectGroup_SameDeviceTwice_ReturnsEmptyGroup()
    {
        var deviceId = DeviceId.New();
        var incoming = MakeDetection(deviceId, BaseLocation, BaseTime);
        var candidate = MakeDetection(deviceId, BaseLocation, BaseTime.AddSeconds(1));

        var group = DetectionGrouper.SelectGroup(incoming, [candidate], Rules);

        Assert.Empty(group);
    }

    [Fact]
    public void SelectGroup_TwoDistinctDevicesWithinWindow_ReturnsGroup()
    {
        var incoming = MakeDetection(DeviceId.New(), BaseLocation, BaseTime);
        var candidate = MakeDetection(DeviceId.New(), BaseLocation, BaseTime.AddSeconds(1));

        var group = DetectionGrouper.SelectGroup(incoming, [candidate], Rules);

        Assert.Equal(2, group.Count);
        Assert.Contains(incoming, group);
        Assert.Contains(candidate, group);
    }

    [Fact]
    public void SelectGroup_ExcludesCandidateWithSameIdAsIncoming()
    {
        var incoming = MakeDetection(DeviceId.New(), BaseLocation, BaseTime);

        var group = DetectionGrouper.SelectGroup(incoming, [incoming], Rules);

        Assert.Empty(group);
    }
}
