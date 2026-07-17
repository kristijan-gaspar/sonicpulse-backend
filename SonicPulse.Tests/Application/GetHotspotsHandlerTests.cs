using Microsoft.Extensions.Time.Testing;
using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Hotspots.Handlers;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Application;

public class GetHotspotsHandlerTests
{
    private static Hotspot MakeHotspot() => Hotspot.Create(
        new Coordinates(45.8021, 15.9711), 142.7, 70, 2,
        new DateTime(2026, 7, 2, 14, 31, 8, DateTimeKind.Utc),
        new DateTime(2026, 7, 2, 14, 31, 11, DateTimeKind.Utc));

    [Fact]
    public async Task HandleAsync_MapsAllFields()
    {
        var hotspot = MakeHotspot();
        var hotspots = new Mock<IHotspotRepository>();
        hotspots.Setup(r => r.GetSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Hotspot>)[hotspot]);

        var handler = new GetHotspotsHandler(hotspots.Object, new FakeTimeProvider());

        var result = await handler.HandleAsync(24, default);

        var dto = Assert.Single(result);
        Assert.Equal(hotspot.Id, dto.Id);
        Assert.Equal(hotspot.Centroid.Latitude, dto.Latitude);
        Assert.Equal(hotspot.Centroid.Longitude, dto.Longitude);
        Assert.Equal(hotspot.RadiusMeters, dto.RadiusMeters);
        Assert.Equal(hotspot.Confidence, dto.Confidence);
        Assert.Equal(hotspot.DeviceCount, dto.DeviceCount);
        Assert.Equal(hotspot.FirstReceivedAtUtc, dto.FirstReceivedAtUtc);
        Assert.Equal(hotspot.LastReceivedAtUtc, dto.LastReceivedAtUtc);
    }

    [Theory]
    [InlineData(0, 1)] // omitted/zero clamps to the 1-hour floor, not "everything"
    [InlineData(24, 24)] // within range, passes through unchanged
    [InlineData(999, 168)] // clamps to the 7-day ceiling
    public async Task HandleAsync_ClampsSinceHoursToOneToOneSixtyEightRange(
        int requestedHours, int expectedClampedHours)
    {
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2026, 7, 2, 14, 31, 7, TimeSpan.Zero));

        DateTime? sinceUtc = null;
        var hotspots = new Mock<IHotspotRepository>();
        hotspots.Setup(r => r.GetSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((s, _) => sinceUtc = s)
            .ReturnsAsync((IReadOnlyList<Hotspot>)[]);

        var handler = new GetHotspotsHandler(hotspots.Object, fakeTime);

        await handler.HandleAsync(requestedHours, default);

        Assert.Equal(
            fakeTime.GetUtcNow().UtcDateTime - TimeSpan.FromHours(expectedClampedHours),
            sinceUtc);
    }
}
