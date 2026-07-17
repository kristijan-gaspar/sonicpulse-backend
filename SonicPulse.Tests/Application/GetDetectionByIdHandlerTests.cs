using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Application;

public class GetDetectionByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_UnknownId_ReturnsNull()
    {
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Detection?)null);

        var handler = new GetDetectionByIdHandler(detections.Object);

        var result = await handler.HandleAsync(Guid.NewGuid(), default);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_KnownId_ReturnsDtoWithAllFieldsMapped()
    {
        var receivedAtUtc = new DateTime(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
        var peakTimeClient = receivedAtUtc.AddSeconds(-2);
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, receivedAtUtc, peakTimeClient);
        var hotspotId = Guid.NewGuid();
        detection.AssignToHotspot(hotspotId);

        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByIdAsync(detection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detection);

        var handler = new GetDetectionByIdHandler(detections.Object);

        var dto = await handler.HandleAsync(detection.Id, default);

        Assert.NotNull(dto);
        Assert.Equal(detection.Id, dto!.Id);
        Assert.Equal(detection.SequenceNumber, dto.SequenceNumber);
        Assert.Equal(detection.DeviceId.Value, dto.DeviceId);
        Assert.Equal(detection.PeakDbfs, dto.PeakDbfs);
        Assert.Equal(detection.Location.Latitude, dto.Latitude);
        Assert.Equal(detection.Location.Longitude, dto.Longitude);
        Assert.Equal(detection.GpsAccuracy, dto.GpsAccuracy);
        Assert.Equal(receivedAtUtc, dto.ReceivedAtUtc);
        Assert.Equal(peakTimeClient, dto.PeakTimeClient);
        Assert.Equal(hotspotId, dto.HotspotId);
    }

    [Fact]
    public async Task HandleAsync_DetectionWithoutHotspot_ReturnsNullHotspotId()
    {
        var detection = Detection.Create(
            DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null);

        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByIdAsync(detection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detection);

        var handler = new GetDetectionByIdHandler(detections.Object);

        var dto = await handler.HandleAsync(detection.Id, default);

        Assert.Null(dto!.HotspotId);
    }
}
