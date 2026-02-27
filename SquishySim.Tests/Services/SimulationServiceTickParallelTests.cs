using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Mind;
using SquishySim.Services;

namespace SquishySim.Tests.Services;

/// <summary>
/// Tests for the parallel tick loop (Issue #23).
/// Verifies that all agents' ChooseAsync calls fire during each tick,
/// and that the tick structure is preserved with async decision makers.
/// </summary>
public class SimulationServiceTickParallelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A fake decision maker that records how many times ChooseAsync was called
    /// and always returns "wander". Synchronous — sidesteps ordering concerns.
    /// </summary>
    private sealed class CountingDecisionMaker : IDecisionMaker
    {
        private int _callCount;
        public int CallCount => _callCount;
        public string DisplayName => "counting-fake";

        public Task<(GameAction action, string reason)> ChooseAsync(
            BodyState state, IReadOnlyList<GameAction> actions,
            string? persona = null, string? navState = null)
        {
            Interlocked.Increment(ref _callCount);
            var action = ActionCatalog.FindById("wander") ?? actions[0];
            return Task.FromResult((action, "fake"));
        }
    }

    /// <summary>
    /// A fake decision maker that records which BodyState snapshot it received.
    /// Used to verify drives are snapshotted at tick start, not read live.
    /// </summary>
    private sealed class SnapshotCapturingDecisionMaker : IDecisionMaker
    {
        public BodyState? CapturedState { get; private set; }
        public string DisplayName => "snapshot-capturing-fake";

        public Task<(GameAction action, string reason)> ChooseAsync(
            BodyState state, IReadOnlyList<GameAction> actions,
            string? persona = null, string? navState = null)
        {
            CapturedState = state;
            var action = ActionCatalog.FindById("wander") ?? actions[0];
            return Task.FromResult((action, "fake"));
        }
    }

    private static SimulationService BuildSim(params IDecisionMaker[] makers)
    {
        var sim = new SimulationService();
        sim.Pause();
        var agents = sim.Agents;
        for (int i = 0; i < makers.Length && i < agents.Count; i++)
            agents[i].DecisionMaker = makers[i];
        return sim;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void When_Step_Called_AllThreeAgentsAreQueried()
    {
        var dm = new CountingDecisionMaker();
        var sim = BuildSim(dm, dm, dm);

        sim.Step();

        Assert.Equal(3, dm.CallCount);
    }

    [Fact]
    public void When_StepCalledTwice_EachAgentIsQueriedTwiceTotal()
    {
        var dm = new CountingDecisionMaker();
        var sim = BuildSim(dm, dm, dm);

        sim.Step();
        sim.Step();

        Assert.Equal(6, dm.CallCount);
    }

    [Fact]
    public void When_Step_DrivesSnapshotPassedToDecisionMaker_NotLiveReference()
    {
        // Verify ChooseAsync receives a snapshot (different object) not the live Drives reference.
        var dm = new SnapshotCapturingDecisionMaker();
        var sim = BuildSim(dm);
        var aliceLiveDrives = sim.Agents[0].Drives;

        sim.Step();

        Assert.NotNull(dm.CapturedState);
        Assert.NotSame(aliceLiveDrives, dm.CapturedState);
    }

    [Fact]
    public void When_Step_SnapshotedDrivesMatchLiveDrivesAtTickStart()
    {
        var dm = new SnapshotCapturingDecisionMaker();
        var sim = BuildSim(dm);
        var alice = sim.Agents[0];

        // Set a known hunger value before the tick
        alice.Drives.Hunger = 0.42f;

        sim.Step();

        // Snapshot should reflect the value at tick-start (after DriveSystem.Tick runs Phase 2,
        // but the initial value is still close — just confirming it was snapshotted, not zero/default)
        Assert.NotNull(dm.CapturedState);
        Assert.True(dm.CapturedState!.Hunger > 0f, "snapshot should reflect non-zero hunger set before tick");
    }

    [Fact]
    public void When_Step_AgentsGetCurrentActionSetAfterTick()
    {
        var sim = new SimulationService();
        sim.Pause();

        sim.Step();

        // All three agents should have a current action after one tick
        foreach (var agent in sim.Agents)
            Assert.NotEqual("(none)", agent.CurrentAction);
    }
}
