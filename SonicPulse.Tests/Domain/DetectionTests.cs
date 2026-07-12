using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Enums;
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
        Assert.Null(detection.HotspotId);
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

    [Fact]
    public void Create_SetsProcessingStatusToPending()
    {
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);

        Assert.Equal(DetectionProcessingStatus.Pending, detection.ProcessingStatus);
    }

    [Fact]
    public void MarkProcessed_SetsProcessingStatusToProcessed()
    {
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);

        detection.MarkProcessed();

        Assert.Equal(DetectionProcessingStatus.Processed, detection.ProcessingStatus);
    }

    [Fact]
    public void MarkFailed_SetsProcessingStatusToFailed()
    {
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);

        detection.MarkFailed();

        Assert.Equal(DetectionProcessingStatus.Failed, detection.ProcessingStatus);
    }

    [Fact]
    public void AssignToHotspot_SetsHotspotId()
    {
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);
        var hotspotId = Guid.NewGuid();

        detection.AssignToHotspot(hotspotId);

        Assert.Equal(hotspotId, detection.HotspotId);
    }
}
