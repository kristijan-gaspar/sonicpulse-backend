namespace SonicPulse.Application.Detections.Dtos;

public sealed record SubmitDetectionRequest(
    Guid DeviceId, double PeakDbfs,
    double Latitude, double Longitude,
    double GpsAccuracy, DateTime? PeakTimeClient);
