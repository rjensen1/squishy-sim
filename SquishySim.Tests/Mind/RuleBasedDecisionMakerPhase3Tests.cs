using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Mind;

namespace SquishySim.Tests.Mind;

public class RuleBasedDecisionMakerPhase3Tests
{
    private static readonly RuleBasedDecisionMaker _maker = new();
    private static readonly IReadOnlyList<GameAction> _actions = ActionCatalog.All;

    private static async Task<string> Choose(BodyState state)
    {
        var (action, _) = await _maker.ChooseAsync(state, _actions);
        return action.Id;
    }

    // ── AC1: Modifier active when Social < 0.65f ─────────────────────────────

    [Fact]
    public async Task WhenSocialBelow0_65_EffectiveHungerThresholdRaisedTo0_90()
    {
        // Social = 0.50f → modifier active → effective hunger = min(0.70 + 0.20, 1.0) = 0.90
        // 0.90 is NOT > 0.90 (strict) → hunger action does not fire
        var state = new BodyState { Social = 0.50f, Hunger = 0.90f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task WhenSocialBelow0_65_HungerAt0_91_ActionFires()
    {
        // 0.91 > 0.90 (effective threshold) → hunger action fires
        var state = new BodyState { Social = 0.50f, Hunger = 0.91f };

        Assert.Equal("eat_food", await Choose(state));
    }

    // ── AC2: Modifier inactive when Social >= 0.65f ──────────────────────────

    [Fact]
    public async Task WhenSocialAt0_65_ModifierInactive_BaseThresholdApplies()
    {
        // Social = 0.65f → condition is strict <, so modifier is OFF → base hunger = 0.70f
        // 0.71 > 0.70 → hunger action fires
        var state = new BodyState { Social = 0.65f, Hunger = 0.71f };

        Assert.Equal("eat_food", await Choose(state));
    }

    // ── AC3: Boundary — Social 0.64f vs 0.65f ────────────────────────────────

    [Fact]
    public async Task WhenSocialAt0_64_EffectiveHungerIs0_90_HungerAt0_85_DoesNotFire()
    {
        // Social = 0.64f → modifier active → effective = 0.90f; 0.85 not > 0.90
        var state = new BodyState { Social = 0.64f, Hunger = 0.85f };

        Assert.NotEqual("eat_food", await Choose(state));
    }

    [Fact]
    public async Task WhenSocialAt0_65_BaseHungerThreshold_HungerAt0_75_Fires()
    {
        // Social = 0.65f → modifier inactive → base = 0.70f; 0.75 > 0.70
        var state = new BodyState { Social = 0.65f, Hunger = 0.75f };

        Assert.Equal("eat_food", await Choose(state));
    }

    // ── AC4: Bladder cap — Math.Min(0.80 + 0.20, 1.0) = 1.00 ───────────────

    [Fact]
    public async Task WhenSocialBelow0_65_EffectiveBladderThresholdCappedAt1_00()
    {
        // Effective = min(0.80 + 0.20, 1.0) = 1.00; 1.00 not > 1.00 (strict)
        var state = new BodyState { Social = 0.50f, Bladder = 1.00f };

        Assert.NotEqual("use_toilet", await Choose(state));
    }

    [Fact]
    public async Task WhenSocialAt0_65_ModifierInactive_BladderAt0_81_Fires()
    {
        // Modifier inactive → base = 0.80f; 0.81 > 0.80
        var state = new BodyState { Social = 0.65f, Bladder = 0.81f };

        Assert.Equal("use_toilet", await Choose(state));
    }

    // ── Modifier applies to all four physical drives ─────────────────────────

    [Fact]
    public async Task WhenModifierActive_ThirstAt0_85_DoesNotFire()
    {
        // Effective thirst = min(0.70 + 0.20, 1.0) = 0.90; 0.85 not > 0.90
        var state = new BodyState { Social = 0.50f, Thirst = 0.85f };

        Assert.NotEqual("drink_water", await Choose(state));
    }

    [Fact]
    public async Task WhenModifierActive_FatigueAt0_90_DoesNotFire()
    {
        // Effective fatigue = min(0.75 + 0.20, 1.0) = 0.95; 0.90 not > 0.95
        var state = new BodyState { Social = 0.50f, Fatigue = 0.90f };

        Assert.NotEqual("sleep", await Choose(state));
    }

    [Fact]
    public async Task WhenModifierActive_FatigueAt0_96_Fires()
    {
        // 0.96 > 0.95 (effective fatigue threshold)
        var state = new BodyState { Social = 0.50f, Fatigue = 0.96f };

        Assert.Equal("sleep", await Choose(state));
    }

    // ── Mood and Social thresholds are NOT modified ───────────────────────────

    [Fact]
    public async Task WhenModifierActive_SocialThresholdUnchanged_SocializeStillFires()
    {
        // Social > 0.65f → socialize fires regardless of modifier (modifier requires Social < 0.65f,
        // so this state has modifier inactive anyway — confirms socialize threshold not shifted)
        var state = new BodyState { Social = 0.70f };

        Assert.Equal("socialize", await Choose(state));
    }
}
