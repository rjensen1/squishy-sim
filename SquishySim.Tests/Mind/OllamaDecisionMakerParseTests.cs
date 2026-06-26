using SquishySim.Actions;
using SquishySim.Mind;

namespace SquishySim.Tests.Mind;

/// <summary>
/// Unit tests for OllamaDecisionMaker.TryParseDecision — the pure parse/validate step,
/// exercised without a running Ollama. Covers plain drive actions, the parameterized
/// "talk" verb (target + content), and every rejection path the repair loop relies on.
/// </summary>
public class OllamaDecisionMakerParseTests
{
    private static readonly IReadOnlyList<GameAction> _actions = ActionCatalog.All;
    private static readonly IReadOnlyList<string> _targets = new[] { "Bob", "Charlie" };

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlainDriveAction_Succeeds()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"eat_food\", \"reason\": \"starving\"}",
            _actions, _targets, out var d, out _);

        Assert.True(ok);
        Assert.Equal("eat_food", d!.Action.Id);
        Assert.Equal("starving", d.Reason);
        Assert.Null(d.Target);
        Assert.Null(d.Content);
    }

    [Fact]
    public void Parse_IgnoresProseAroundJson()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "Sure! Here's the choice:\n{\"action\": \"sleep\", \"reason\": \"tired\"}\nHope that helps.",
            _actions, _targets, out var d, out _);

        Assert.True(ok);
        Assert.Equal("sleep", d!.Action.Id);
    }

    [Fact]
    public void Parse_Talk_WithTargetAndContent_Succeeds()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"talk\", \"target\": \"Bob\", \"content\": \"Hey, found water by the rocks.\", \"reason\": \"sharing\"}",
            _actions, _targets, out var d, out _);

        Assert.True(ok);
        Assert.Equal("talk", d!.Action.Id);
        Assert.Equal("Bob", d.Target);
        Assert.Equal("Hey, found water by the rocks.", d.Content);
        Assert.Equal("sharing", d.Reason);
    }

    [Fact]
    public void Parse_Talk_TargetMatchIsCaseInsensitive_AndCanonicalizes()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"talk\", \"target\": \"bob\", \"content\": \"hi\", \"reason\": \"lonely\"}",
            _actions, _targets, out var d, out _);

        Assert.True(ok);
        Assert.Equal("Bob", d!.Target);   // canonical casing from the legal set
    }

    // ── Rejection paths (each must give the repair loop a reason) ──────────────

    [Fact]
    public void Parse_NoJson_Fails()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "I think Dana should probably eat something.",
            _actions, _targets, out var d, out var reason);

        Assert.False(ok);
        Assert.Null(d);
        Assert.Contains("no JSON", reason);
    }

    [Fact]
    public void Parse_UnknownAction_Fails()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"fly_to_paris\", \"reason\": \"wanderlust\"}",
            _actions, _targets, out var d, out var reason);

        Assert.False(ok);
        Assert.Null(d);
        Assert.Contains("fly_to_paris", reason);
    }

    [Fact]
    public void Parse_Talk_MissingTarget_Fails()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"talk\", \"content\": \"hi there\", \"reason\": \"lonely\"}",
            _actions, _targets, out var d, out var reason);

        Assert.False(ok);
        Assert.Null(d);
        Assert.Contains("target", reason);
    }

    [Fact]
    public void Parse_Talk_MissingContent_Fails()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"talk\", \"target\": \"Bob\", \"reason\": \"lonely\"}",
            _actions, _targets, out var d, out var reason);

        Assert.False(ok);
        Assert.Null(d);
        Assert.Contains("content", reason);
    }

    [Fact]
    public void Parse_Talk_TargetNotInLegalSet_Fails()
    {
        var ok = OllamaDecisionMaker.TryParseDecision(
            "{\"action\": \"talk\", \"target\": \"Zelda\", \"content\": \"hi\", \"reason\": \"x\"}",
            _actions, _targets, out var d, out var reason);

        Assert.False(ok);
        Assert.Null(d);
        Assert.Contains("Zelda", reason);
    }
}
