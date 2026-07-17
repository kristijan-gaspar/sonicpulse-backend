using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class HotspotTests
{
    private static readonly Coordinates Centroid = new(45.8010, 15.9700);
    private static readonly DateTime First = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
    private static readonly DateTime Last = First.AddSeconds(2);

    [Fact]
    public void Create_ValidValues_PopulatesProperties()
    {
        var hotspot = Hotspot.Create(Centroid, 150.0, 70, 2, First, Last);

        Assert.NotEqual(Guid.Empty, hotspot.Id);
        Assert.Equal(Centroid, hotspot.Centroid);
        Assert.Equal(150.0, hotspot.RadiusMeters);
        Assert.Equal(70, hotspot.Confidence);
        Assert.Equal(2, hotspot.DeviceCount);
        Assert.Equal(First, hotspot.FirstReceivedAtUtc);
        Assert.Equal(Last, hotspot.LastReceivedAtUtc);
    }

    [Fact]
    public void Create_NegativeRadius_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Hotspot.Create(Centroid, -1, 70, 2, First, Last));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ConfidenceOutOfRange_Throws(int confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Hotspot.Create(Centroid, 150.0, confidence, 2, First, Last));
    }

    [Fact]
    public void Create_DeviceCountBelowTwo_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Hotspot.Create(Centroid, 150.0, 70, 1, First, Last));
    }

    [Fact]
    public void Create_FirstReceivedAfterLastReceived_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Hotspot.Create(Centroid, 150.0, 70, 2, Last, First));
    }

    [Fact]
    public void Recalculate_MutatesExistingObject()
    {
        var hotspot = Hotspot.Create(Centroid, 150.0, 70, 2, First, Last);
        var id = hotspot.Id;

        var newCentroid = new Coordinates(45.8100, 15.9800);
        var newLast = Last.AddSeconds(1);

        hotspot.Recalculate(newCentroid, 200.0, 55, 3, First, newLast);

        Assert.Equal(id, hotspot.Id);
        Assert.Equal(newCentroid, hotspot.Centroid);
        Assert.Equal(200.0, hotspot.RadiusMeters);
        Assert.Equal(55, hotspot.Confidence);
        Assert.Equal(3, hotspot.DeviceCount);
        Assert.Equal(newLast, hotspot.LastReceivedAtUtc);
    }

    [Fact]
    public void Recalculate_InvalidValues_Throws()
    {
        var hotspot = Hotspot.Create(Centroid, 150.0, 70, 2, First, Last);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => hotspot.Recalculate(Centroid, 150.0, 70, 1, First, Last));
    }
}
