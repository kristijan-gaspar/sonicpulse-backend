using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class CoordinatesTests
{
    [Theory]
    [InlineData(45.8010, 15.9700)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Constructor_ValidRanges_Succeeds(double latitude, double longitude)
    {
        var coordinates = new Coordinates(latitude, longitude);

        Assert.Equal(latitude, coordinates.Latitude);
        Assert.Equal(longitude, coordinates.Longitude);
    }

    [Fact]
    public void Constructor_LatitudeOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordinates(91, 15.9700));
    }

    [Fact]
    public void Constructor_LongitudeOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coordinates(45.8010, -181));
    }
}
