using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Services;

public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double Meters(Coordinates a, Coordinates b)
    {
        double dLat = ToRadians(b.Latitude - a.Latitude);
        double dLon = ToRadians(b.Longitude - a.Longitude);

        double sinLat = Math.Sin(dLat / 2);
        double sinLon = Math.Sin(dLon / 2);

        double h = sinLat * sinLat
                 + Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude))
                 * sinLon * sinLon;

        h = Math.Clamp(h, 0.0, 1.0);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(h));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
