using Microsoft.Extensions.Time.Testing;
using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Tests.Application;

public class SubmitDetectionHandlerTests
{
    private static readonly SubmitDetectionRequest Request = new(
        DeviceId: Guid.NewGuid(),
        PeakDbfs: -8.5,
        Latitude: 45.8010,
        Longitude: 15.9700,
        GpsAccuracy: 12.0,
        PeakTimeClient: null);

    [Fact]
    public async Task HandleAsync_UsesTimeProviderForReceivedAtUtc_NotClientTime()
    {
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2026, 7, 2, 14, 31, 7, TimeSpan.Zero));

        Detection? saved = null;
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.AddAsync(It.IsAny<Detection>(), It.IsAny<CancellationToken>()))
            .Callback<Detection, CancellationToken>((d, _) => saved = d)
            .Returns(Task.CompletedTask);

        var queue = new Mock<IDetectionProcessingQueue>();
        var handler = new SubmitDetectionHandler(detections.Object, queue.Object, fakeTime);

        await handler.HandleAsync(Request, default);

        Assert.NotNull(saved);
        Assert.Equal(fakeTime.GetUtcNow().UtcDateTime, saved!.ReceivedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_EnqueuesTheSavedDetectionId()
    {
        var fakeTime = new FakeTimeProvider();
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.AddAsync(It.IsAny<Detection>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var queue = new Mock<IDetectionProcessingQueue>();
        var handler = new SubmitDetectionHandler(detections.Object, queue.Object, fakeTime);

        var response = await handler.HandleAsync(Request, default);

        queue.Verify(q => q.EnqueueAsync(response.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
