using System.Reflection;
using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Application;

public class GetDetectionsByDeviceHandlerTests
{
    // SequenceNumber is DB-assigned (private setter, not a Create() parameter
    // by design). Repository fakes/mocks in these tests stand in for
    // already-persisted rows, so we need to give them a value the same way
    // EF materialization would.
    private static Detection WithSequenceNumber(Detection detection, long sequenceNumber)
    {
        typeof(Detection).GetProperty(nameof(Detection.SequenceNumber))!
            .SetValue(detection, sequenceNumber);
        return detection;
    }

    private static Detection MakeDetection(long sequenceNumber)
        => WithSequenceNumber(
            Detection.Create(DeviceId.New(), -8.5, new Coordinates(45.8010, 15.9700), 12.0, DateTime.UtcNow, null),
            sequenceNumber);

    [Fact]
    public async Task HandleAsync_LimitBelowMinimum_ClampsToOne()
    {
        long? capturedLimit = null;
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByDeviceIdAsync(
                It.IsAny<DeviceId>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceId, long?, int, CancellationToken>((_, _, limit, _) => capturedLimit = limit)
            .ReturnsAsync((IReadOnlyList<Detection>)[]);

        var handler = new GetDetectionsByDeviceHandler(detections.Object);

        await handler.HandleAsync(Guid.NewGuid(), null, limit: 0, default);

        Assert.Equal(1, capturedLimit);
    }

    [Fact]
    public async Task HandleAsync_LimitAboveMaximum_ClampsTo100()
    {
        long? capturedLimit = null;
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByDeviceIdAsync(
                It.IsAny<DeviceId>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceId, long?, int, CancellationToken>((_, _, limit, _) => capturedLimit = limit)
            .ReturnsAsync((IReadOnlyList<Detection>)[]);

        var handler = new GetDetectionsByDeviceHandler(detections.Object);

        await handler.HandleAsync(Guid.NewGuid(), null, limit: 500, default);

        Assert.Equal(100, capturedLimit);
    }

    [Fact]
    public async Task HandleAsync_MoreRowsThanLimit_TrimsPageAndSetsNextCursorFromLastReturnedItem()
    {
        // Repository returns limit + 1 rows as the "has more" signal (per
        // IDetectionRepository's contract - see plan branch 1, §4.1).
        IReadOnlyList<Detection> rows = [MakeDetection(10), MakeDetection(9), MakeDetection(8)];
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByDeviceIdAsync(
                It.IsAny<DeviceId>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var handler = new GetDetectionsByDeviceHandler(detections.Object);

        var result = await handler.HandleAsync(Guid.NewGuid(), null, limit: 2, default);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(9, result.NextCursor); // last item of the TRIMMED page, not the discarded 3rd row
    }

    [Fact]
    public async Task HandleAsync_RowsEqualToLimit_NoNextCursor()
    {
        IReadOnlyList<Detection> rows = [MakeDetection(10), MakeDetection(9)];
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByDeviceIdAsync(
                It.IsAny<DeviceId>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var handler = new GetDetectionsByDeviceHandler(detections.Object);

        var result = await handler.HandleAsync(Guid.NewGuid(), null, limit: 2, default);

        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task HandleAsync_NoDetectionsForDevice_ReturnsEmptyPage()
    {
        var detections = new Mock<IDetectionRepository>();
        detections.Setup(r => r.GetByDeviceIdAsync(
                It.IsAny<DeviceId>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Detection>)[]);

        var handler = new GetDetectionsByDeviceHandler(detections.Object);

        var result = await handler.HandleAsync(Guid.NewGuid(), null, limit: 50, default);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }
}
