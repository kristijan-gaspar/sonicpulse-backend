namespace SonicPulse.Application.Detections.Dtos;

public sealed record DetectionDto(
    Guid Id, long SequenceNumber, Guid DeviceId, double PeakDbfs,
    double Latitude, double Longitude, double GpsAccuracy,
    DateTime ReceivedAtUtc, DateTime? PeakTimeClient);
