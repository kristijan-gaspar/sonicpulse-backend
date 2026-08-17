namespace SonicPulse.Application.Hotspots.Dtos;

public sealed record HotspotDto(
    Guid Id, double Latitude, double Longitude, double RadiusMeters,
    int DeviceCount, DateTime FirstReceivedAtUtc, DateTime LastReceivedAtUtc);
