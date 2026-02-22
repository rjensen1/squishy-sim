using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

public interface IDecisionMaker
{
    string DisplayName { get; }

    Task<(GameAction action, string reason)> ChooseAsync(
        BodyState state,
        IReadOnlyList<GameAction> actions);
}
