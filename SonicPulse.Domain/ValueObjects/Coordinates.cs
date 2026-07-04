namespace SonicPulse.Domain.ValueObjects;

public readonly record struct Coordinates
{
    public double Latitude { get; }
    public double Longitude { get; }

    public Coordinates(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude),
                "Latitude must be within [-90, 90].");
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude),
                "Longitude must be within [-180, 180].");

        Latitude = latitude;
        Longitude = longitude;
    }
}
