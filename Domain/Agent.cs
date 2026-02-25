using SquishySim.Body;
using SquishySim.Mind;

namespace SquishySim.Domain;

// PROTOTYPE: Agent domain model — wraps biological drives + decision-making + history
public class Agent
{
    public string Id   { get; init; } = "";
    public string Name { get; init; } = "";

    public BodyState Drives { get; init; } = new();

    public string CurrentAction { get; set; } = "(none)";
    public string CurrentReason { get; set; } = "";

    public List<ThoughtEntry>       Thoughts      { get; } = new();
    public List<ConversationMessage> Messages     { get; } = new(); // messages involving this agent

    public LlmConfig       LlmConfig     { get; set; } = new();
    public IDecisionMaker  DecisionMaker { get; set; } = new RuleBasedDecisionMaker();

    // Set by SimulationService when a socialize action succeeds this tick;
    // consumed by DriveSystem.Tick the following tick to apply social satisfaction.
    public bool HadSocialInteractionLastTick { get; set; } = false;
}
