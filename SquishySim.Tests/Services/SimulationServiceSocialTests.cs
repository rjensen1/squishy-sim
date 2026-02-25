using SquishySim.Domain;
using SquishySim.Services;

namespace SquishySim.Tests.Services;

/// <summary>
/// Integration tests for social drive behavior in SimulationService.
/// AC7: Socialize action succeeds when partner is available.
/// AC8: Socialize action fails gracefully when no partner available.
/// Note: SimulationService always seeds 3 agents (alice, bob, charlie),
/// so there is always a partner available in the normal case.
/// </summary>
public class SimulationServiceSocialTests
{
    // ── AC7: Partner found — conversation generated, satisfaction next tick ──
    // Phase 3.5 note: socialize is now navigation-based. Tests place agents within
    // SocialRange (2.5f) so the interaction fires within a single Step().

    [Fact]
    public void WhenAgentSocializesWith_PartnerAvailable_ConversationIsGenerated()
    {
        var sim   = new SimulationService();
        var alice = sim.GetAgent("alice")!;
        var bob   = sim.GetAgent("bob")!;

        // Place alice and bob within SocialRange so interaction fires this tick
        alice.Position = (5f, 5f);
        bob.Position   = (6f, 5f);   // distance = 1.0f < SocialRange(2.5f)

        sim.SetDrive("alice", "social",  0.80);
        sim.SetDrive("alice", "hunger",  0.10);
        sim.SetDrive("alice", "thirst",  0.10);
        sim.SetDrive("alice", "fatigue", 0.10);
        sim.SetDrive("alice", "bladder", 0.10);

        // Seed alice as Seeking bob so movement phase fires
        alice.NavState     = NavigationState.Seeking;
        alice.SeekTargetId = "bob";
        alice.Destination  = bob.Position;

        sim.Step();

        var conversations = sim.AllConversations;
        Assert.NotEmpty(conversations);
        Assert.Contains(conversations, c => c.FromAgentId == "alice");
    }

    [Fact]
    public void WhenSocializeSucceeds_AgentCurrentActionIs_Socialize()
    {
        var sim   = new SimulationService();
        var alice = sim.GetAgent("alice")!;
        var bob   = sim.GetAgent("bob")!;

        alice.Position = (5f, 5f);
        bob.Position   = (6f, 5f);

        sim.SetDrive("alice", "social",  0.80);
        sim.SetDrive("alice", "hunger",  0.10);
        sim.SetDrive("alice", "thirst",  0.10);
        sim.SetDrive("alice", "fatigue", 0.10);
        sim.SetDrive("alice", "bladder", 0.10);

        alice.NavState     = NavigationState.Seeking;
        alice.SeekTargetId = "bob";
        alice.Destination  = bob.Position;

        sim.Step();

        Assert.Equal("socialize", alice.CurrentAction);
    }

    [Fact]
    public void WhenSocializeSucceeds_SocialDriveDecreasesOnFollowingTick()
    {
        var sim   = new SimulationService();
        var alice = sim.GetAgent("alice")!;
        var bob   = sim.GetAgent("bob")!;

        alice.Position = (5f, 5f);
        bob.Position   = (6f, 5f);

        sim.SetDrive("alice", "social",  0.80);
        sim.SetDrive("alice", "hunger",  0.10);
        sim.SetDrive("alice", "thirst",  0.10);
        sim.SetDrive("alice", "fatigue", 0.10);
        sim.SetDrive("alice", "bladder", 0.10);

        alice.NavState     = NavigationState.Seeking;
        alice.SeekTargetId = "bob";
        alice.Destination  = bob.Position;

        // Tick 1: interaction fires — HadSocialInteractionLastTick set on both
        sim.Step();
        var socialAfterTick1 = sim.GetAgent("alice")!.Drives.Social;

        // Tick 2: DriveSystem sees hadQualifyingInteraction=true — applies satisfaction (-0.25f)
        sim.Step();
        var socialAfterTick2 = sim.GetAgent("alice")!.Drives.Social;

        Assert.True(socialAfterTick2 < socialAfterTick1,
            $"Expected social to decrease after satisfaction tick. " +
            $"Tick1={socialAfterTick1:F4}, Tick2={socialAfterTick2:F4}");
    }

    [Fact]
    public void WhenSocializeSucceeds_PartnerAlsoReceivesSocialSatisfactionNextTick()
    {
        var sim   = new SimulationService();
        var alice = sim.GetAgent("alice")!;
        var bob   = sim.GetAgent("bob")!;

        alice.Position = (5f, 5f);
        bob.Position   = (6f, 5f);

        sim.SetDrive("alice", "social",  0.80);
        sim.SetDrive("alice", "hunger",  0.10);
        sim.SetDrive("alice", "thirst",  0.10);
        sim.SetDrive("alice", "fatigue", 0.10);
        sim.SetDrive("alice", "bladder", 0.10);

        alice.NavState     = NavigationState.Seeking;
        alice.SeekTargetId = "bob";
        alice.Destination  = bob.Position;

        // Tick 1: interaction fires — both agents get HadSocialInteractionLastTick
        sim.Step();

        const string partnerId = "bob";
        sim.SetDrive(partnerId, "hunger",  0.10);
        sim.SetDrive(partnerId, "thirst",  0.10);
        sim.SetDrive(partnerId, "fatigue", 0.10);
        sim.SetDrive(partnerId, "bladder", 0.10);

        var partnerSocialAfterTick1 = sim.GetAgent(partnerId)!.Drives.Social;

        sim.Step();  // Tick 2: partner gets satisfaction

        var partnerSocialAfterTick2 = sim.GetAgent(partnerId)!.Drives.Social;
        Assert.True(partnerSocialAfterTick2 < partnerSocialAfterTick1,
            $"Expected partner {partnerId} social to decrease after satisfaction. " +
            $"Tick1={partnerSocialAfterTick1:F4}, Tick2={partnerSocialAfterTick2:F4}");
    }

    // ── AC6: Background chatter does NOT satisfy social drive ───────────────

    [Fact]
    public void BackgroundChatterDoesNotSatisfySocialDrive()
    {
        // Background chatter fires every 5 ticks via MaybeGenerateConversations.
        // It must not set HadSocialInteractionLastTick, so social drive keeps decaying.
        var sim = new SimulationService();

        // Keep social below the socialize trigger so only background chatter can fire
        sim.SetDrive("alice", "social",  0.40);
        sim.SetDrive("alice", "hunger",  0.10);
        sim.SetDrive("alice", "thirst",  0.10);
        sim.SetDrive("alice", "fatigue", 0.10);
        sim.SetDrive("alice", "bladder", 0.10);

        var socialBefore = sim.GetAgent("alice")!.Drives.Social;

        // Run 5 ticks to guarantee background chatter fires
        for (int i = 0; i < 5; i++) sim.Step();

        var socialAfter = sim.GetAgent("alice")!.Drives.Social;

        // Social should have increased (decay applied), not decreased (satisfaction)
        Assert.True(socialAfter > socialBefore,
            $"Expected social to increase via decay (no qualifying interaction). " +
            $"Before={socialBefore:F4}, After={socialAfter:F4}");
    }

    // ── Verify SetDrive accepts "social" ─────────────────────────────────────

    [Fact]
    public void WhenSetDriveCalledWithSocial_DriveValueIsUpdated()
    {
        var sim = new SimulationService();

        var result = sim.SetDrive("alice", "social", 0.75);

        Assert.True(result);
        Assert.Equal(0.75f, sim.GetAgent("alice")!.Drives.Social, precision: 4);
    }
}
