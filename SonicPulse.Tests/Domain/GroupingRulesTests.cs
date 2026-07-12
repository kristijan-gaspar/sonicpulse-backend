using SonicPulse.Domain.Rules;

namespace SonicPulse.Tests.Domain;

public class GroupingRulesTests
{
    [Fact]
    public void Constructor_ValidValues_Succeeds()
    {
        var rules = new GroupingRules(
            timeWindow: TimeSpan.FromSeconds(5),
            radiusMeters: 1000,
            minDeviceCount: 2,
            candidateSearchExpansionFactor: 1.1);

        Assert.Equal(TimeSpan.FromSeconds(5), rules.TimeWindow);
        Assert.Equal(1000, rules.RadiusMeters);
        Assert.Equal(2, rules.MinDeviceCount);
        Assert.Equal(1.1, rules.CandidateSearchExpansionFactor);
    }

    [Fact]
    public void Constructor_ZeroTimeWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupingRules(TimeSpan.Zero, 1000, 2, 1.1));
    }

    [Fact]
    public void Constructor_NegativeRadius_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupingRules(TimeSpan.FromSeconds(5), -1, 2, 1.1));
    }

    [Fact]
    public void Constructor_MinDeviceCountBelowTwo_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupingRules(TimeSpan.FromSeconds(5), 1000, 1, 1.1));
    }

    [Fact]
    public void Constructor_ExpansionFactorBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupingRules(TimeSpan.FromSeconds(5), 1000, 2, 0.9));
    }
}
