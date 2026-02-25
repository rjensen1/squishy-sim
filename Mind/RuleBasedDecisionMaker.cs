using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

/// <summary>
/// Deterministic fallback decision maker.
/// Useful for development/testing without a running Ollama instance.
/// Implements a simple urgency priority queue.
/// </summary>
public class RuleBasedDecisionMaker : IDecisionMaker
{
    public string DisplayName => "rule-based";

    // Context modifier: when Social < SocialTriggerThreshold, agent is socially engaged —
    // physical-drive action thresholds are raised by ContextModifier (capped at 1.0).
    private const float ContextModifier       = 0.20f;
    private const float SocialTriggerThreshold = 0.65f;

    public Task<(GameAction action, string reason)> ChooseAsync(
        BodyState state, IReadOnlyList<GameAction> actions)
    {
        float mod = state.Social < SocialTriggerThreshold ? ContextModifier : 0f;

        if (state.Bladder > Math.Min(0.80f + mod, 1.0f))
            return Done("use_toilet", "bladder is urgent");

        if (state.Thirst > Math.Min(0.70f + mod, 1.0f))
            return Done("drink_water", "thirst is high");

        if (state.Hunger > Math.Min(0.70f + mod, 1.0f))
            return Done("eat_food", "hunger is high");

        if (state.Fatigue > Math.Min(0.75f + mod, 1.0f))
            return Done("sleep", "fatigue is high");

        if (state.Social > SocialTriggerThreshold)
            return Done("socialize", "feeling isolated");

        if (state.Mood < 0.35f)
            return Done("wander", "mood is low — needs stimulation");

        return Done("wander", "all needs met");
    }

    private static Task<(GameAction action, string reason)> Done(string id, string reason)
    {
        var action = ActionCatalog.FindById(id) ?? ActionCatalog.All[0];
        return Task.FromResult((action, reason));
    }
}
