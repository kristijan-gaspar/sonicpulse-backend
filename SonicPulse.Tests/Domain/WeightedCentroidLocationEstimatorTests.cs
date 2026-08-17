using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class WeightedCentroidLocationEstimatorTests
{
    private static readonly DateTime BaseTime = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);

    private static Detection MakeDetection(double peakDbfs, double gpsAccuracy, Coordinates location)
        => Detection.Create(DeviceId.New(), peakDbfs, location, gpsAccuracy, BaseTime, null);

    [Fact]
    public void Estimate_EqualWeights_ReturnsMidpoint()
    {
        var a = MakeDetection(-10, 10, new Coordinates(45.0, 15.0));
        var b = MakeDetection(-10, 10, new Coordinates(46.0, 16.0));

        var centroid = WeightedCentroidLocationEstimator.Estimate([a, b]);

        Assert.Equal(45.5, centroid.Latitude, precision: 6);
        Assert.Equal(15.5, centroid.Longitude, precision: 6);
    }

    [Fact]
    public void Estimate_EqualAccuracyDifferentPeakDbfs_StillReturnsMidpoint()
    {
        // dBFS is not comparable across uncalibrated microphones (see
        // SonicPulse-algoritmi-detekcije.md §2.3) - weight comes from GPS
        // accuracy only, so a much louder reading must NOT skew the centroid.
        var loud = MakeDetection(-5, 10, new Coordinates(45.0, 15.0));
        var quiet = MakeDetection(-40, 10, new Coordinates(46.0, 16.0));

        var centroid = WeightedCentroidLocationEstimator.Estimate([loud, quiet]);

        Assert.Equal(45.5, centroid.Latitude, precision: 6);
        Assert.Equal(15.5, centroid.Longitude, precision: 6);
    }

    [Fact]
    public void Estimate_MoreAccurateDetection_PullsCentroidTowardItself()
    {
        var precise = MakeDetection(-10, 2, new Coordinates(45.0, 15.0));
        var imprecise = MakeDetection(-10, 50, new Coordinates(46.0, 16.0));

        var centroid = WeightedCentroidLocationEstimator.Estimate([precise, imprecise]);

        Assert.True(centroid.Latitude < 45.5);
        Assert.True(centroid.Longitude < 15.5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Estimate_NonPositiveGpsAccuracy_Throws(double gpsAccuracy)
    {
        var a = MakeDetection(-10, gpsAccuracy, new Coordinates(45.0, 15.0));
        var b = MakeDetection(-10, 10, new Coordinates(46.0, 16.0));

        Assert.Throws<ArgumentException>(
            () => WeightedCentroidLocationEstimator.Estimate([a, b]));
    }

    [Fact]
    public void Estimate_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => WeightedCentroidLocationEstimator.Estimate([]));
    }
}
