using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Domain;
using SquishySim.Mind;
using SquishySim.Services;

namespace SquishySim.Tests.Services;

/// <summary>
/// End-to-end coverage for the "talk" verb: a decision maker that emits talk + target +
/// content should produce a ConversationMessage carrying the LLM's exact line, and grant
/// social credit to both parties.
/// </summary>
public class SimulationServiceTalkTests
{
    /// <summary>Always chooses "talk" aimed at a fixed agent with a fixed line.</summary>
    private sealed class TalkingDecisionMaker : IDecisionMaker
    {
        private readonly string _targetName;
        private readonly string _line;
        public TalkingDecisionMaker(string targetName, string line)
        {
            _targetName = targetName;
            _line = line;
        }

        public string DisplayName => "talking-fake";

        public Task<DecisionResult> ChooseAsync(
            BodyState state, IReadOnlyList<GameAction> actions,
            string? persona = null, string? navState = null,
            IReadOnlyList<string>? targets = null)
        {
            var talk = ActionCatalog.FindById("talk")!;
            return Task.FromResult(new DecisionResult(talk, "wanted to chat", _targetName, _line));
        }
    }

    [Fact]
    public void Talk_RecordsConversationMessage_WithLlmContent()
    {
        var sim = new SimulationService();
        sim.Pause();
        var alice = sim.Agents[0];
        var bob   = sim.Agents[1];
        alice.DecisionMaker = new TalkingDecisionMaker(bob.Name, "Hey Bob, found water by the rocks.");

        sim.Step();

        var msg = sim.AllConversations.FirstOrDefault(
            m => m.FromAgentId == alice.Id && m.ToAgentId == bob.Id);

        Assert.NotNull(msg);
        Assert.Equal("Hey Bob, found water by the rocks.", msg!.Text);
    }

    [Fact]
    public void Talk_GrantsSocialCreditToBothParties()
    {
        var sim = new SimulationService();
        sim.Pause();
        var alice = sim.Agents[0];
        var bob   = sim.Agents[1];
        alice.DecisionMaker = new TalkingDecisionMaker(bob.Name, "Hi.");

        sim.Step();

        Assert.True(alice.HadSocialInteractionLastTick);
        Assert.True(bob.HadSocialInteractionLastTick);
    }

    [Fact]
    public void Talk_AtUnknownTarget_RecordsNoMessage()
    {
        var sim = new SimulationService();
        sim.Pause();
        var alice = sim.Agents[0];
        alice.DecisionMaker = new TalkingDecisionMaker("Nobody", "Hello?");

        sim.Step();

        Assert.DoesNotContain(sim.AllConversations, m => m.FromAgentId == alice.Id);
    }
}
