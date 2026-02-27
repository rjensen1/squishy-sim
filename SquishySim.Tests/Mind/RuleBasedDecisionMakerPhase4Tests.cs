using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Mind;

namespace SquishySim.Tests.Mind;

public class RuleBasedDecisionMakerPhase4Tests
{
    private static readonly RuleBasedDecisionMaker _maker = new();
    private static readonly IReadOnlyList<GameAction> _actions = ActionCatalog.All;

    private static async Task<string> Choose(BodyState state)
    {
        var (action, _) = await _maker.ChooseAsync(state, _actions);
        return action.Id;
    }

    // ── AC1: High budget (budget > 0.50f) — same behavior as Phase 3 ─────────

    [Fact]
    public async Task AC1_HighBudget_FullModifierApplied_HungerAt0_90_DoesNotFire()
    {
        // Social = 0.20f (engaged), budget = 0.80f → full modifier (0.20f)
        // Effective hunger = min(0.70 + 0.20, 1.0) = 0.90; 0.90 is NOT > 0.90 (strict)
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.80f, Hunger = 0.90f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task AC1_HighBudget_FullModifierApplied_HungerAt0_91_Fires()
    {
        // 0.91 > 0.90 (effective threshold with full modifier)
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.80f, Hunger = 0.91f };

        Assert.Equal("eat_food", await Choose(state));
    }

    // ── AC2: Medium budget (0.25f < budget <= 0.50f) — modifier halved ───────

    [Fact]
    public async Task AC2_MediumBudget_HalfModifierApplied_HungerAt0_79_DoesNotFire()
    {
        // Budget = 0.40f → half modifier (0.10f); effective hunger = min(0.70 + 0.10, 1.0) = 0.80
        // 0.79 is NOT > 0.80 (strict); social is irrelevant at this tier
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.40f, Hunger = 0.79f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task AC2_MediumBudget_HalfModifierApplied_HungerAt0_81_Fires()
    {
        // 0.81 > 0.80 (effective threshold with half modifier)
        // Social value is irrelevant at medium budget — ComputeModifier does not check social below high
        var state = new BodyState { Social = 0.90f, SuppressionBudget = 0.40f, Hunger = 0.81f };

        Assert.Equal("eat_food", await Choose(state));
    }

    [Fact]
    public async Task AC2_MediumBudget_SocialValueIrrelevant_SameThresholdRegardlessOfSocialState()
    {
        // Budget = 0.40f → half modifier regardless of social state
        // Both engaged (Social=0.20) and isolated (Social=0.90) should give effective hunger = 0.80f
        var engaged  = new BodyState { Social = 0.20f, SuppressionBudget = 0.40f, Hunger = 0.79f };
        var isolated = new BodyState { Social = 0.90f, SuppressionBudget = 0.40f, Hunger = 0.79f };

        Assert.NotEqual("eat_food", await Choose(engaged));
        Assert.NotEqual("eat_food", await Choose(isolated));
    }

    // ── AC3: Low budget (0 < budget <= 0.25f) — no modifier ─────────────────

    [Fact]
    public async Task AC3_LowBudget_NoModifier_HungerAt0_71_Fires()
    {
        // Budget = 0.15f → modifier = 0; raw threshold = 0.70f; 0.71 > 0.70 → fires
        // Social value is irrelevant at low budget
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.15f, Hunger = 0.71f };

        Assert.Equal("eat_food", await Choose(state));
    }

    [Fact]
    public async Task AC3_LowBudget_NoModifier_SocialValueIrrelevant_RawThresholdApplies()
    {
        // Budget = 0.15f → same threshold whether social engaged or isolated
        var engaged  = new BodyState { Social = 0.20f, SuppressionBudget = 0.15f, Hunger = 0.71f };
        var isolated = new BodyState { Social = 0.90f, SuppressionBudget = 0.15f, Hunger = 0.71f };

        Assert.Equal("eat_food", await Choose(engaged));
        Assert.Equal("eat_food", await Choose(isolated));
    }

    // ── AC4a: Snap, decision layer — pure unit test on RuleBasedDecisionMaker ─

    [Fact]
    public async Task AC4a_SnapBudget_NoModifier_AgentActsOnRawThresholdRegardlessOfSocialState()
    {
        // Budget = 0f → modifier = 0; raw threshold = 0.70f; 0.71 > 0.70 → fires
        // Social = 0.20f (would normally activate full modifier) — irrelevant at snap
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0f, Hunger = 0.71f };

        Assert.Equal("eat_food", await Choose(state));
    }

    [Fact]
    public async Task AC4a_SnapBudget_ContextModifierNotApplied_HungerAt0_90_Fires()
    {
        // Budget = 0f → no modifier; 0.90 > 0.70 (raw) → fires even though it would be suppressed at high budget
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0f, Hunger = 0.90f };

        Assert.Equal("eat_food", await Choose(state));
    }

    // ── Tier boundary tests ──────────────────────────────────────────────────

    [Fact]
    public async Task BudgetAt0_25f_IsLowTier_NoModifier()
    {
        // Budget = 0.25f is still low tier (≤ 0.25f); modifier = 0
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.25f, Hunger = 0.71f };

        Assert.Equal("eat_food", await Choose(state));
    }

    [Fact]
    public async Task BudgetJustAbove0_25f_IsMediumTier_HalfModifierApplied()
    {
        // Budget = 0.26f is medium tier (> 0.25f, ≤ 0.50f); half modifier (0.10f)
        // Effective hunger = 0.80f; hunger = 0.75 < 0.80 → does NOT fire
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.26f, Hunger = 0.75f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task BudgetAt0_50f_IsMediumTier_HalfModifierApplied()
    {
        // Budget = 0.50f is still medium tier (≤ 0.50f)
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.50f, Hunger = 0.75f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task BudgetJustAbove0_50f_IsHighTier_FullModifierApplied()
    {
        // Budget = 0.51f is high tier (> 0.50f); full modifier (0.20f) applies when social engaged
        // Effective hunger = 0.90f; hunger = 0.85f < 0.90f → does NOT fire
        var state = new BodyState { Social = 0.20f, SuppressionBudget = 0.51f, Hunger = 0.85f };

        Assert.NotEqual("eat_food", await Choose(state));
    }
}
