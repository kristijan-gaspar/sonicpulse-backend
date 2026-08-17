using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Domain.Services;

public static class WeightedCentroidLocationEstimator
{
    public static Coordinates Estimate(IReadOnlyList<Detection> detections)
    {
        if (detections.Count == 0)
            throw new ArgumentException(
                "Cannot estimate centroid from an empty detection list.", nameof(detections));

        double sumW = 0, sumLat = 0, sumLon = 0;
        foreach (var d in detections)
        {
            if (d.GpsAccuracy <= 0)
                throw new ArgumentException(
                    "GpsAccuracy must be positive to compute an inverse-square weight.", nameof(detections));

            double w = 1.0 / (d.GpsAccuracy * d.GpsAccuracy);

            sumW += w;
            sumLat += w * d.Location.Latitude;
            sumLon += w * d.Location.Longitude;
        }

        if (sumW <= 0)
            throw new ArgumentException(
                "Total weight underflowed to zero; cannot compute a centroid.", nameof(detections));

        return new Coordinates(sumLat / sumW, sumLon / sumW);
    }
}
