namespace SonicPulse.Domain.Rules;

public sealed record GroupingRules
{
    public TimeSpan TimeWindow { get; }
    public double RadiusMeters { get; }
    public int MinDeviceCount { get; }
    public double CandidateSearchExpansionFactor { get; }

    public GroupingRules(
        TimeSpan timeWindow, double radiusMeters,
        int minDeviceCount, double candidateSearchExpansionFactor)
    {
        if (timeWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeWindow));
        if (radiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        if (minDeviceCount < 2)
            throw new ArgumentOutOfRangeException(nameof(minDeviceCount),
                "Single-detection hotspots are invalid by definition.");
        if (candidateSearchExpansionFactor < 1.0)
            throw new ArgumentOutOfRangeException(nameof(candidateSearchExpansionFactor));

        TimeWindow = timeWindow;
        RadiusMeters = radiusMeters;
        MinDeviceCount = minDeviceCount;
        CandidateSearchExpansionFactor = candidateSearchExpansionFactor;
    }
}
