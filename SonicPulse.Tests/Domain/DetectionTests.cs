using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class DetectionTests
{
    [Fact]
    public void Create_PopulatesAllProperties()
    {
        var deviceId = DeviceId.New();
        var location = new Coordinates(45.8010, 15.9700);
        var receivedAtUtc = new DateTime(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
        var peakTimeClient = receivedAtUtc.AddSeconds(-2);

        var detection = Detection.Create(deviceId, -8.5, location, 12.0, receivedAtUtc, peakTimeClient);

        Assert.NotEqual(Guid.Empty, detection.Id);
        Assert.Equal(deviceId, detection.DeviceId);
        Assert.Equal(-8.5, detection.PeakDbfs);
        Assert.Equal(location, detection.Location);
        Assert.Equal(12.0, detection.GpsAccuracy);
        Assert.Equal(receivedAtUtc, detection.ReceivedAtUtc);
        Assert.Equal(peakTimeClient, detection.PeakTimeClient);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        var deviceId = DeviceId.New();
        var location = new Coordinates(45.8010, 15.9700);
        var receivedAtUtc = DateTime.UtcNow;

        var first = Detection.Create(deviceId, -8.5, location, 12.0, receivedAtUtc, null);
        var second = Detection.Create(deviceId, -8.5, location, 12.0, receivedAtUtc, null);

        Assert.NotEqual(first.Id, second.Id);
    }
}
