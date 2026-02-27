using SquishySim.Body;

namespace SquishySim.Tests.Body;

public class DriveSystemPhase4Tests
{
    // ── AC5: SnappedAt cleared on recovery above 0.25f ───────────────────────

    [Fact]
    public void AC5_WhenBudgetRecoveryAbove0_25f_SnappedAtIsCleared()
    {
        // Start snapped: budget = 0, SnappedAt set
        var state = new BodyState
        {
            SuppressionBudget = 0f,
            SnappedAt = DateTime.UtcNow.AddMinutes(-1),
            // All drives comfortable → triggers regen
            Hunger = 0.10f, Thirst = 0.10f, Fatigue = 0.10f, Bladder = 0.10f
        };

        // Tick until budget crosses above 0.25f
        for (int i = 0; i < 15; i++)
            DriveSystem.Tick(state);

        Assert.True(state.SuppressionBudget > 0.25f, "Budget should recover above 0.25f with comfortable drives");
        Assert.Null(state.SnappedAt);
    }

    [Fact]
    public void SnappedAt_IsSetWhenBudgetFirstHitsZero()
    {
        var state = new BodyState
        {
            SuppressionBudget = 1.0f,
            SnappedAt = null,
            // Compound pressure to deplete budget
            Hunger = 0.75f, Fatigue = 0.75f
        };

        // Tick until budget depletes to 0
        for (int i = 0; i < 25; i++)
            DriveSystem.Tick(state);

        Assert.NotNull(state.SnappedAt);
    }

    [Fact]
    public void SnappedAt_NotSetUntilBudgetActuallyHitsZero()
    {
        // Budget starts high but not yet depleted to zero
        var state = new BodyState
        {
            SuppressionBudget = 0.30f,
            SnappedAt = null,
            Hunger = 0.75f, Fatigue = 0.75f  // compound pressure depletes fast
        };

        // One tick should deplete below 0.25f but SnappedAt only set when budget <= 0
        DriveSystem.Tick(state);

        // budget may still be > 0 — SnappedAt should only be set at exactly <= 0f
        if (state.SuppressionBudget > 0f)
            Assert.Null(state.SnappedAt);
    }

    // ── AC6: Budget floor — never goes below 0f ───────────────────────────────

    [Fact]
    public void AC6_BudgetNeverGoesNegativeUnderAnyPressure()
    {
        var state = new BodyState
        {
            SuppressionBudget = 0f,
            Hunger = 0.90f, Thirst = 0.90f, Fatigue = 0.90f, Bladder = 0.90f
        };

        // Apply many ticks of maximum pressure
        for (int i = 0; i < 50; i++)
            DriveSystem.Tick(state);

        Assert.True(state.SuppressionBudget >= 0f, "SuppressionBudget must never go below 0f");
    }

    // ── Depletion: compound pressure depletes faster than single critical ────

    [Fact]
    public void CompoundPressure_DepletesMoreThanSingleCritical_OverSameTicks()
    {
        var compound = new BodyState
        {
            SuppressionBudget = 1.0f,
            Hunger = 0.75f, Fatigue = 0.75f  // compound trigger
        };
        var singleCrit = new BodyState
        {
            SuppressionBudget = 1.0f,
            Hunger = 0.90f,  // single critical only
            Fatigue = 0.10f
        };

        for (int i = 0; i < 5; i++)
        {
            DriveSystem.Tick(compound);
            DriveSystem.Tick(singleCrit);
        }

        Assert.True(compound.SuppressionBudget < singleCrit.SuppressionBudget,
            "Compound pressure should deplete budget faster than single critical");
    }

    // ── Regen: comfortable state regenerates budget ──────────────────────────

    [Fact]
    public void ComfortableState_RegeneratesBudget()
    {
        var state = new BodyState
        {
            SuppressionBudget = 0.30f,
            Hunger = 0.10f, Thirst = 0.10f, Fatigue = 0.10f, Bladder = 0.10f
        };
        var before = state.SuppressionBudget;

        DriveSystem.Tick(state);

        Assert.True(state.SuppressionBudget > before, "Budget should increase in comfortable state");
    }

    [Fact]
    public void NonComfortableNonCriticalState_BudgetUnchanged()
    {
        // Drives between 0.40f and 0.70f — no depletion condition, no regen
        var state = new BodyState
        {
            SuppressionBudget = 0.60f,
            Hunger = 0.50f, Thirst = 0.50f, Fatigue = 0.50f, Bladder = 0.50f
        };
        var before = state.SuppressionBudget;

        DriveSystem.Tick(state);

        // No change expected (no compound, no single critical, not comfortable)
        Assert.Equal(before, state.SuppressionBudget, precision: 4);
    }
}
