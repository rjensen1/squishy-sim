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

    public Task<(GameAction action, string reason)> ChooseAsync(
        BodyState state, IReadOnlyList<GameAction> actions)
    {
        if (state.Bladder > 0.80f)
            return Done("use_toilet", "bladder is urgent");

        if (state.Thirst > 0.70f)
            return Done("drink_water", "thirst is high");

        if (state.Hunger > 0.70f)
            return Done("eat_food", "hunger is high");

        if (state.Fatigue > 0.75f)
            return Done("sleep", "fatigue is high");

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
