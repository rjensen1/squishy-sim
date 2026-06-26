using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

public interface IDecisionMaker
{
    string DisplayName { get; }

    Task<DecisionResult> ChooseAsync(
        BodyState state,
        IReadOnlyList<GameAction> actions,
        string? persona = null,
        string? navState = null,
        IReadOnlyList<string>? targets = null);  // names this agent may aim a "talk" at
}
