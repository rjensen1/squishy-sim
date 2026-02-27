using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Mind;

namespace SquishySim.Tests.Mind;

public class PromptBuilderTests
{
    private static readonly IReadOnlyList<GameAction> _actions = ActionCatalog.All;

    // ── Social drive ──────────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludesSocialDrive()
    {
        var state = new BodyState { Social = 0.65f };
        var prompt = PromptBuilder.Build(state, _actions);
        Assert.Contains("social", prompt);
        Assert.Contains("0.65", prompt);
    }

    // ── SuppressionBudget ─────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludesSuppressionBudget()
    {
        var state = new BodyState { SuppressionBudget = 0.42f };
        var prompt = PromptBuilder.Build(state, _actions);
        Assert.Contains("budget", prompt);
        Assert.Contains("0.42", prompt);
    }

    // ── Snap state ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludesSnappedWarning_WhenSnapped()
    {
        var state = new BodyState { SnappedAt = DateTime.UtcNow };
        var prompt = PromptBuilder.Build(state, _actions);
        Assert.Contains("SNAPPED", prompt);
    }

    [Fact]
    public void Build_NoSnappedWarning_WhenNotSnapped()
    {
        var state = new BodyState { SnappedAt = null };
        var prompt = PromptBuilder.Build(state, _actions);
        Assert.DoesNotContain("SNAPPED", prompt);
    }

    // ── Persona ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludesPersona_WhenProvided()
    {
        var state = new BodyState();
        var persona = "You tend to hold your needs longer than you should.";
        var prompt = PromptBuilder.Build(state, _actions, persona: persona);
        Assert.Contains(persona, prompt);
    }

    [Fact]
    public void Build_NoPersonaSection_WhenPersonaNull()
    {
        var state = new BodyState();
        var prompt = PromptBuilder.Build(state, _actions, persona: null);
        Assert.DoesNotContain("disposition", prompt);
    }

    [Fact]
    public void Build_NoPersonaSection_WhenPersonaEmpty()
    {
        var state = new BodyState();
        var prompt = PromptBuilder.Build(state, _actions, persona: "");
        Assert.DoesNotContain("disposition", prompt);
    }

    // ── NavState ──────────────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludesNavState_WhenProvided()
    {
        var state = new BodyState();
        var prompt = PromptBuilder.Build(state, _actions, navState: "Committed");
        Assert.Contains("movement", prompt);
        Assert.Contains("Committed", prompt);
    }

    [Fact]
    public void Build_NoNavSection_WhenNavStateNull()
    {
        var state = new BodyState();
        var prompt = PromptBuilder.Build(state, _actions, navState: null);
        Assert.DoesNotContain("movement", prompt);
    }

    // ── Core drives still present ─────────────────────────────────────────────

    [Fact]
    public void Build_IncludesCoreDrivers()
    {
        var state = new BodyState { Hunger = 0.55f, Thirst = 0.30f, Fatigue = 0.70f };
        var prompt = PromptBuilder.Build(state, _actions);
        Assert.Contains("hunger", prompt);
        Assert.Contains("thirst", prompt);
        Assert.Contains("fatigue", prompt);
        Assert.Contains("bladder", prompt);
        Assert.Contains("mood", prompt);
    }
}
