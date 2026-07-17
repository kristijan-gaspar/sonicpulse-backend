using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Application.Detections.Validators;

namespace SonicPulse.Tests.Application;

public class SubmitDetectionRequestValidatorTests
{
    private static readonly SubmitDetectionRequestValidator Validator = new();

    private static SubmitDetectionRequest ValidRequest() => new(
        DeviceId: Guid.NewGuid(),
        PeakDbfs: -8.5,
        Latitude: 45.8010,
        Longitude: 15.9700,
        GpsAccuracy: 12.0,
        PeakTimeClient: null);

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = Validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyDeviceId_Fails()
    {
        var request = ValidRequest() with { DeviceId = Guid.Empty };

        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(5.0)]
    public void Validate_PositivePeakDbfs_Fails(double peakDbfs)
    {
        var request = ValidRequest() with { PeakDbfs = peakDbfs };

        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ZeroPeakDbfs_Passes()
    {
        var request = ValidRequest() with { PeakDbfs = 0 };

        var result = Validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(91)]
    [InlineData(-91)]
    public void Validate_LatitudeOutOfRange_Fails(double latitude)
    {
        var request = ValidRequest() with { Latitude = latitude };

        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-181)]
    public void Validate_LongitudeOutOfRange_Fails(double longitude)
    {
        var request = ValidRequest() with { Longitude = longitude };

        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveGpsAccuracy_Fails(double gpsAccuracy)
    {
        var request = ValidRequest() with { GpsAccuracy = gpsAccuracy };

        var result = Validator.Validate(request);

        Assert.False(result.IsValid);
    }
}
