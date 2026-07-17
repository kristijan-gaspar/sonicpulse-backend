namespace SonicPulse.Api.Configuration;

public sealed class GroupingOptions
{
    public const string SectionName = "Grouping";

    public double TimeWindowSeconds { get; set; }
    public double RadiusMeters { get; set; }
    public int MinDeviceCount { get; set; }
    public double CandidateSearchExpansionFactor { get; set; }
}