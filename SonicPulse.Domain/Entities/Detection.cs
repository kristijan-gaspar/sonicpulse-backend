using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Entities;

public class Detection
{
    public Guid Id { get; private set; }
    public long SequenceNumber { get; private set; } 
    public DeviceId DeviceId { get; private set; }
    public double PeakDbfs { get; private set; }
    public Coordinates Location { get; private set; }
    public double GpsAccuracy { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? PeakTimeClient { get; private set; }

    private Detection() { } 

    public static Detection Create(
        DeviceId deviceId, double peakDbfs, Coordinates location,
        double gpsAccuracy, DateTime receivedAtUtc, DateTime? peakTimeClient)
    {
        return new Detection
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            PeakDbfs = peakDbfs,
            Location = location,
            GpsAccuracy = gpsAccuracy,
            ReceivedAtUtc = receivedAtUtc,
            PeakTimeClient = peakTimeClient
        };
    }
}
