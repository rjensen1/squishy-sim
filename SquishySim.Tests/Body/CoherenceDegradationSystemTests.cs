using SquishySim.Body;

namespace SquishySim.Tests.Body;

public class CoherenceDegradationSystemTests
{
    // ── Below threshold: no compression ──────────────────────────────────────

    [Fact]
    public void WhenSocialBelowOnsetThreshold_DrivesAreUnchanged()
    {
        var snapshot = new BodyState
        {
            Hunger = 0.9f, Thirst = 0.1f, Fatigue = 0.3f, Bladder = 0.2f,
            Social = 0.29f  // just below 0.3 onset threshold
        };

        var result = CoherenceDegradationSystem.ApplyToSnapshot(snapshot);

        Assert.Equal(0.9f, result.Hunger,  precision: 4);
        Assert.Equal(0.1f, result.Thirst,  precision: 4);
        Assert.Equal(0.3f, result.Fatigue, precision: 4);
        Assert.Equal(0.2f, result.Bladder, precision: 4);
    }

    [Fact]
    public void WhenSocialAtOnsetThreshold_DrivesAreUnchanged()
    {
        var snapshot = new BodyState
        {
            Hunger = 0.8f, Thirst = 0.2f, Fatigue = 0.5f, Bladder = 0.1f,
            Social = 0.30f  // exactly at threshold → isolationFactor = 0
        };

        var result = CoherenceDegradationSystem.ApplyToSnapshot(snapshot);

        Assert.Equal(0.8f, result.Hunger,  precision: 4);
        Assert.Equal(0.2f, result.Thirst,  precision: 4);
        Assert.Equal(0.5f, result.Fatigue, precision: 4);
        Assert.Equal(0.1f, result.Bladder, precision: 4);
    }

    // ── Above threshold: drives compress toward mean ──────────────────────────

    [Fact]
    public void WhenSocialAboveThreshold_DrivesCompressTowardMean()
    {
        // Hunger is high, Thirst is low — under isolation, they should converge
        var snapshot = new BodyState
        {
            Hunger = 0.9f, Thirst = 0.1f, Fatigue = 0.1f, Bladder = 0.1f,
            Social = 0.65f
        };
        float meanBefore = (0.9f + 0.1f + 0.1f + 0.1f) / 4f;  // 0.3

        var result = CoherenceDegradationSystem.ApplyToSnapshot(snapshot);

        // Hunger should decrease toward mean; Thirst should increase toward mean
        Assert.True(result.Hunger < 0.9f, "Hunger should compress downward toward mean");
        Assert.True(result.Thirst > 0.1f, "Thirst should compress upward toward mean");
        // Mean of compressed values should remain the same
        float meanAfter = (result.Hunger + result.Thirst + result.Fatigue + result.Bladder) / 4f;
        Assert.Equal(meanBefore, meanAfter, precision: 4);
    }

    // ── Full isolation: drives equal mean ────────────────────────────────────

    [Fact]
    public void WhenSocialAtMaximum_AllDrivesEqualMean()
    {
        var snapshot = new BodyState
        {
            Hunger = 0.9f, Thirst = 0.1f, Fatigue = 0.5f, Bladder = 0.3f,
            Social = 1.0f  // maximum isolation → isolationFactor = 1.0
        };
        float expectedMean = (0.9f + 0.1f + 0.5f + 0.3f) / 4f;  // 0.45

        var result = CoherenceDegradationSystem.ApplyToSnapshot(snapshot);

        Assert.Equal(expectedMean, result.Hunger,  precision: 4);
        Assert.Equal(expectedMean, result.Thirst,  precision: 4);
        Assert.Equal(expectedMean, result.Fatigue, precision: 4);
        Assert.Equal(expectedMean, result.Bladder, precision: 4);
    }

    // ── Social, Mood, SuppressionBudget are not modified ─────────────────────

    [Fact]
    public void ApplyToSnapshot_DoesNotModifySocialMoodOrBudget()
    {
        var snapshot = new BodyState
        {
            Hunger = 0.9f, Thirst = 0.1f, Fatigue = 0.5f, Bladder = 0.3f,
            Social = 0.9f,
            Mood = 0.6f,
            SuppressionBudget = 0.75f
        };

        var result = CoherenceDegradationSystem.ApplyToSnapshot(snapshot);

        Assert.Equal(0.9f,  result.Social,            precision: 4);
        Assert.Equal(0.6f,  result.Mood,              precision: 4);
        Assert.Equal(0.75f, result.SuppressionBudget, precision: 4);
    }

    // ── BehavioralCoherence: ranges correctly ─────────────────────────────────

    [Fact]
    public void BehavioralCoherence_IsOneWhenSocialBelowThreshold()
    {
        Assert.Equal(1.0f, CoherenceDegradationSystem.BehavioralCoherence(0.0f),  precision: 4);
        Assert.Equal(1.0f, CoherenceDegradationSystem.BehavioralCoherence(0.30f), precision: 4);
    }

    [Fact]
    public void BehavioralCoherence_IsZeroAtMaxIsolation()
    {
        Assert.Equal(0.0f, CoherenceDegradationSystem.BehavioralCoherence(1.0f), precision: 4);
    }

    [Fact]
    public void BehavioralCoherence_DecreasesMonotonicallyWithIsolation()
    {
        float prev = CoherenceDegradationSystem.BehavioralCoherence(0.3f);
        foreach (var social in new[] { 0.4f, 0.5f, 0.65f, 0.8f, 1.0f })
        {
            float current = CoherenceDegradationSystem.BehavioralCoherence(social);
            Assert.True(current <= prev, $"Coherence should decrease as Social increases (at {social})");
            prev = current;
        }
    }
}
