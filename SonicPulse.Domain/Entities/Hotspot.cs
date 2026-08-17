using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Entities;

public sealed class Hotspot
{
    public Guid Id { get; private set; }
    public Coordinates Centroid { get; private set; }
    public double RadiusMeters { get; private set; }
    public int DeviceCount { get; private set; }
    public DateTime FirstReceivedAtUtc { get; private set; }
    public DateTime LastReceivedAtUtc { get; private set; }

    private Hotspot() { }

    public static Hotspot Create(
        Coordinates centroid,
        double radiusMeters,
        int deviceCount,
        DateTime firstReceivedAtUtc,
        DateTime lastReceivedAtUtc)
    {
        var hotspot = new Hotspot
        {
            Id = Guid.NewGuid()
        };

        hotspot.Apply(
            centroid,
            radiusMeters,
            deviceCount,
            firstReceivedAtUtc,
            lastReceivedAtUtc);

        return hotspot;
    }

    public void Recalculate(
        Coordinates centroid,
        double radiusMeters,
        int deviceCount,
        DateTime firstReceivedAtUtc,
        DateTime lastReceivedAtUtc)
    {
        Apply(
            centroid,
            radiusMeters,
            deviceCount,
            firstReceivedAtUtc,
            lastReceivedAtUtc);
    }

    private void Apply(
        Coordinates centroid,
        double radiusMeters,
        int deviceCount,
        DateTime firstReceivedAtUtc,
        DateTime lastReceivedAtUtc)
    {
        Validate(
            radiusMeters,
            deviceCount,
            firstReceivedAtUtc,
            lastReceivedAtUtc);

        Centroid = centroid;
        RadiusMeters = radiusMeters;
        DeviceCount = deviceCount;
        FirstReceivedAtUtc = firstReceivedAtUtc;
        LastReceivedAtUtc = lastReceivedAtUtc;
    }

    private static void Validate(
        double radiusMeters,
        int deviceCount,
        DateTime firstReceivedAtUtc,
        DateTime lastReceivedAtUtc)
    {
        if (radiusMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));

        if (deviceCount < 2)
            throw new ArgumentOutOfRangeException(
                nameof(deviceCount),
                "A hotspot requires at least two distinct devices.");

        if (firstReceivedAtUtc > lastReceivedAtUtc)
            throw new ArgumentException(
                "First received time must not be after last received time.");
    }
}
