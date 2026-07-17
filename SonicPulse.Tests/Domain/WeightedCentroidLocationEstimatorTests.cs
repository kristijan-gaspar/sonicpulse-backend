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
    public void Estimate_LouderDetection_PullsCentroidTowardItself()
    {
        var loud = MakeDetection(-5, 10, new Coordinates(45.0, 15.0));
        var quiet = MakeDetection(-20, 10, new Coordinates(46.0, 16.0));

        var centroid = WeightedCentroidLocationEstimator.Estimate([loud, quiet]);

        Assert.True(centroid.Latitude < 45.5);
        Assert.True(centroid.Longitude < 15.5);
    }

    [Fact]
    public void Estimate_GpsAccuracyOfZero_DoesNotThrow()
    {
        var a = MakeDetection(-10, 0, new Coordinates(45.0, 15.0));
        var b = MakeDetection(-10, 10, new Coordinates(46.0, 16.0));

        var centroid = WeightedCentroidLocationEstimator.Estimate([a, b]);

        Assert.InRange(centroid.Latitude, 45.0, 46.0);
    }

    [Fact]
    public void Estimate_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => WeightedCentroidLocationEstimator.Estimate([]));
    }

    [Fact]
    public void Estimate_WeightUnderflowsToZero_ThrowsInsteadOfReturningNaN()
    {
        var a = MakeDetection(-6500, 10, new Coordinates(45.0, 15.0));
        var b = MakeDetection(-6500, 10, new Coordinates(46.0, 16.0));

        Assert.Throws<ArgumentException>(
            () => WeightedCentroidLocationEstimator.Estimate([a, b]));
    }
}
