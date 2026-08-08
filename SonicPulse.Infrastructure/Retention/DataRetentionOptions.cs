namespace SonicPulse.Infrastructure.Retention;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public int RetentionHours { get; set; }
    public int CleanupIntervalHours { get; set; }
}