using SquishySim.Body;
using SquishySim.Domain;
using SquishySim.Mind;

namespace SquishySim.Tests.Mind;

public class CognitiveDriftSystemTests
{
    private static Agent AgentWith(float social, float drift = 0.0f) => new Agent
    {
        Id = "test", Name = "Test",
        Drives = { Social = social },
        PersonaDriftFactor = drift
    };

    // ── Accumulation: drift grows when isolated ───────────────────────────────

    [Fact]
    public void WhenSocialAboveIsolationThreshold_DriftAccumulates()
    {
        var agent = AgentWith(social: 0.6f, drift: 0.0f);  // 0.6 > 0.50 threshold

        CognitiveDriftSystem.Tick(agent);

        Assert.True(agent.PersonaDriftFactor > 0.0f, "Drift should accumulate when isolated");
    }

    [Fact]
    public void WhenSocialAtIsolationThreshold_DriftDoesNotAccumulate()
    {
        var agent = AgentWith(social: 0.50f, drift: 0.2f);  // exactly at threshold

        CognitiveDriftSystem.Tick(agent);

        Assert.Equal(0.2f, agent.PersonaDriftFactor, precision: 4);
    }

    // ── Recovery: drift clears when social ───────────────────────────────────

    [Fact]
    public void WhenSocialBelowRecoveryThreshold_DriftDecreases()
    {
        var agent = AgentWith(social: 0.2f, drift: 0.5f);  // 0.2 < 0.30 recovery threshold

        CognitiveDriftSystem.Tick(agent);

        Assert.True(agent.PersonaDriftFactor < 0.5f, "Drift should decrease when socially active");
    }

    [Fact]
    public void WhenSocialAtRecoveryThreshold_DriftDoesNotRecover()
    {
        var agent = AgentWith(social: 0.30f, drift: 0.3f);  // exactly at recovery threshold

        CognitiveDriftSystem.Tick(agent);

        Assert.Equal(0.3f, agent.PersonaDriftFactor, precision: 4);
    }

    // ── Plateau: between thresholds, drift is unchanged ──────────────────────

    [Fact]
    public void WhenSocialBetweenThresholds_DriftIsUnchanged()
    {
        var agent = AgentWith(social: 0.40f, drift: 0.35f);  // 0.30 < 0.40 < 0.50

        CognitiveDriftSystem.Tick(agent);

        Assert.Equal(0.35f, agent.PersonaDriftFactor, precision: 4);
    }

    // ── Bounds: drift stays within [0, 1] ────────────────────────────────────

    [Fact]
    public void DriftFactor_NeverExceedsOneUnderSustainedIsolation()
    {
        var agent = AgentWith(social: 1.0f, drift: 0.999f);

        for (int i = 0; i < 10; i++)
            CognitiveDriftSystem.Tick(agent);

        Assert.True(agent.PersonaDriftFactor <= 1.0f, "Drift should be capped at 1.0");
    }

    [Fact]
    public void DriftFactor_NeverGoesBelowZeroUnderSustainedRecovery()
    {
        var agent = AgentWith(social: 0.0f, drift: 0.001f);

        for (int i = 0; i < 10; i++)
            CognitiveDriftSystem.Tick(agent);

        Assert.True(agent.PersonaDriftFactor >= 0.0f, "Drift should be floored at 0.0");
    }

    // ── Persona string: drift modifies the returned persona ──────────────────

    [Fact]
    public void BuildDriftedPersona_ReturnsBasePersona_WhenDriftBelowThreshold()
    {
        var base_ = "You tend to hold your needs.";
        Assert.Equal(base_, CognitiveDriftSystem.BuildDriftedPersona(base_, 0.05f));
    }

    [Fact]
    public void BuildDriftedPersona_AppendsLightDriftText_WhenDriftModerate()
    {
        var base_ = "You tend to hold your needs.";
        var result = CognitiveDriftSystem.BuildDriftedPersona(base_, 0.25f);

        Assert.StartsWith(base_, result);
        Assert.True(result!.Length > base_.Length, "Drifted persona should be longer than base");
    }

    [Fact]
    public void BuildDriftedPersona_ReturnsNull_WhenBasePersonaIsNull()
    {
        Assert.Null(CognitiveDriftSystem.BuildDriftedPersona(null, 0.9f));
    }

    [Fact]
    public void BuildDriftedPersona_ReturnsEmpty_WhenBasePersonaIsEmpty()
    {
        Assert.Equal("", CognitiveDriftSystem.BuildDriftedPersona("", 0.9f));
    }
}
