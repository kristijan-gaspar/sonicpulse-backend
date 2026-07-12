using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class GeoDistanceTests
{
    [Fact]
    public void Meters_SamePoint_ReturnsZero()
    {
        var point = new Coordinates(45.8010, 15.9700);

        Assert.Equal(0, GeoDistance.Meters(point, point));
    }

    [Fact]
    public void Meters_OneDegreeOfLatitude_ReturnsExpectedDistance()
    {
        // One degree of latitude along a meridian is ~111,195 m regardless of longitude.
        var a = new Coordinates(0, 0);
        var b = new Coordinates(1, 0);

        var distance = GeoDistance.Meters(a, b);

        Assert.InRange(distance, 111_190, 111_200);
    }

    [Fact]
    public void Meters_KnownZagrebPair_MatchesIndependentlyComputedDistance()
    {
        // Ban Jelacic Square vs Zagreb Cathedral. Expected value independently
        // computed (PowerShell haversine, not this implementation): ~401.15 m.
        var banJelacicSquare = new Coordinates(45.8131, 15.9775);
        var zagrebCathedral = new Coordinates(45.8150, 15.9819);

        var distance = GeoDistance.Meters(banJelacicSquare, zagrebCathedral);

        Assert.InRange(distance, 400.15, 402.15);
    }

    [Fact]
    public void Meters_IsSymmetric()
    {
        var a = new Coordinates(45.8010, 15.9700);
        var b = new Coordinates(45.8100, 15.9800);

        Assert.Equal(GeoDistance.Meters(a, b), GeoDistance.Meters(b, a));
    }
}
