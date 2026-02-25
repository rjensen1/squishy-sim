using SquishySim.Body;

namespace SquishySim.Tests.Body;

public class DriveInteractionSystemTests
{
    // ── AC1: hunger→fatigue rate multiplier ──────────────────────────────────

    [Fact]
    public void HungerAboveThreshold_FatigueRateMultiplierIs1Point20()
    {
        var state = new BodyState { Hunger = 0.65f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.20f, mods.FatigueRateMultiplier);
    }

    [Fact]
    public void HungerBelowThreshold_FatigueRateMultiplierIsOne()
    {
        var state = new BodyState { Hunger = 0.59f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.00f, mods.FatigueRateMultiplier);
    }

    [Fact]
    public void HungerAtExactThreshold_FatigueRateMultiplierApplies()
    {
        // threshold is >=, not > — crossing exactly 0.60f activates the effect
        var state = new BodyState { Hunger = 0.60f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.20f, mods.FatigueRateMultiplier);
    }

    // ── AC2: fatigue→hunger rate multiplier ──────────────────────────────────

    [Fact]
    public void FatigueAboveThreshold_HungerRateMultiplierIs1Point25()
    {
        var state = new BodyState { Fatigue = 0.65f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.25f, mods.HungerRateMultiplier);
    }

    [Fact]
    public void FatigueBelowThreshold_HungerRateMultiplierIsOne()
    {
        var state = new BodyState { Fatigue = 0.59f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.00f, mods.HungerRateMultiplier);
    }

    // ── AC3: compound rule ────────────────────────────────────────────────────

    [Fact]
    public void BothAboveCompoundThreshold_BothMultipliersAreCompound()
    {
        var state = new BodyState { Hunger = 0.75f, Fatigue = 0.75f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.40f, mods.HungerRateMultiplier);
        Assert.Equal(1.40f, mods.FatigueRateMultiplier);
    }

    [Fact]
    public void OnlyHungerAboveCompoundThreshold_IndividualModifiersApply_NotCompound()
    {
        // hunger=0.75 (above compound 0.70), fatigue=0.65 (above individual 0.60, below compound 0.70)
        // compound NOT triggered — individual rules apply
        var state = new BodyState { Hunger = 0.75f, Fatigue = 0.65f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.20f, mods.FatigueRateMultiplier);  // hunger≥0.60 individual rule
        Assert.Equal(1.25f, mods.HungerRateMultiplier);   // fatigue≥0.60 individual rule
    }

    // ── AC4: no cross-effects below all thresholds ───────────────────────────

    [Fact]
    public void BothBelowThresholds_NoCrossEffects()
    {
        var state = new BodyState { Hunger = 0.59f, Fatigue = 0.59f };
        var mods = DriveInteractionSystem.ComputeCrossEffects(state);
        Assert.Equal(1.00f, mods.HungerRateMultiplier);
        Assert.Equal(1.00f, mods.FatigueRateMultiplier);
    }

    // ── AC5: integration — Tick() applies compound modifier end-to-end ───────

    [Fact]
    public void Tick_WithCompoundThreshold_FatigueAccumulatesFasterThanBaseRate()
    {
        const float BaseFatigueRate = 0.012f;
        var state = new BodyState { Hunger = 0.75f, Fatigue = 0.75f };
        var fatigueBefore = state.Fatigue;

        DriveSystem.Tick(state);

        var fatigueDelta = state.Fatigue - fatigueBefore;
        Assert.True(fatigueDelta > BaseFatigueRate,
            $"Expected fatigue delta > {BaseFatigueRate} (base rate) but got {fatigueDelta}");
    }
}
