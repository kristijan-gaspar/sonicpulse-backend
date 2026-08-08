namespace SonicPulse.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}