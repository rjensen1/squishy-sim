using SquishySim.Domain;
using SquishySim.Services;

namespace SquishySim.Tests.Services;

public class SimulationServiceSpatialTests
{
    private static SimulationService MakeSim() => new();

    // ── AC7: Agent moves MoveSpeed per tick toward destination ────────────────

    [Fact]
    public void Step_MovesAgentByMoveSpeedTowardDestination()
    {
        const float MoveSpeed = 1.0f;
        var sim   = MakeSim();
        var alice = sim.GetAgent("alice")!;

        // Place alice far from food; set hunger high so decision maker picks eat_food.
        // Social=0.70 (above SocialTriggerThreshold=0.65) disables Phase 3 context modifier
        // so hunger threshold stays at 0.70 and 0.80 reliably triggers eat_food.
        alice.Position  = (0f, 0f);
        alice.Drives.Hunger  = 0.80f;   // above 0.70f threshold → eat_food
        alice.Drives.Thirst  = 0.10f;
        alice.Drives.Fatigue = 0.10f;
        alice.Drives.Bladder = 0.10f;
        alice.Drives.Social  = 0.70f;   // above SocialTriggerThreshold → context modifier inactive

        sim.Step();

        // Food station is at (5,5); distance from (0,0) = 7.07. After one step:
        // direction = (5/7.07, 5/7.07) = (0.707, 0.707); new pos ≈ (0.707, 0.707)
        var expected = 1.0f / MathF.Sqrt(2f) * MoveSpeed;
        Assert.Equal(expected, alice.Position.X, precision: 3);
        Assert.Equal(expected, alice.Position.Y, precision: 3);
        Assert.Equal(NavigationState.Committed, alice.NavState);
    }

    // ── AC8: Arrival at resource — drive effect applied, returns to Idle ──────

    [Fact]
    public void Step_WhenWithinResourceInteractRadius_AppliesEffectAndGoesIdle()
    {
        const float ResourceInteractRadius = 1.5f;
        var sim   = MakeSim();
        var alice = sim.GetAgent("alice")!;

        // Place alice within interact radius of food station (5,5).
        // Social=0.70 disables Phase 3 context modifier so hunger threshold stays at 0.70
        // and eat_food is chosen; CurrentAction must be "eat_food" for ApplyArrivalEffects to work.
        alice.Position    = (5f, 5f - ResourceInteractRadius + 0.1f);  // just inside radius
        alice.NavState    = NavigationState.Committed;
        alice.Destination = (5f, 5f);
        alice.Drives.Hunger  = 0.80f;   // hunger high → eat_food chosen
        alice.Drives.Thirst  = 0.10f;
        alice.Drives.Fatigue = 0.10f;
        alice.Drives.Bladder = 0.10f;
        alice.Drives.Social  = 0.70f;   // above SocialTriggerThreshold → context modifier inactive

        var hungerBefore = alice.Drives.Hunger;

        sim.Step();

        Assert.Equal(NavigationState.Idle, alice.NavState);
        Assert.Null(alice.Destination);
        Assert.True(alice.Drives.Hunger < hungerBefore,
            $"Expected hunger < {hungerBefore:F2} after eating, got {alice.Drives.Hunger:F2}");
    }

    // ── AC9: Preemption — higher-priority drive reroutes mid-navigation ───────

    [Fact]
    public void Step_WhenHigherPriorityDriveCrossesThreshold_PreemptsNavigation()
    {
        var sim   = MakeSim();
        var alice = sim.GetAgent("alice")!;

        // Alice is currently navigating to water (thirst was high, bladder was low).
        // Social=0.70 disables Phase 3 context modifier so bladder threshold stays at 0.80
        // and 0.85 reliably triggers use_toilet preemption (with modifier active, threshold=1.0).
        alice.Position    = (0f, 0f);
        alice.NavState    = NavigationState.Committed;
        alice.Destination = (15f, 5f);   // water station
        alice.Drives.Thirst  = 0.75f;
        alice.Drives.Bladder = 0.85f;    // bladder above threshold → use_toilet takes priority
        alice.Drives.Hunger  = 0.10f;
        alice.Drives.Fatigue = 0.10f;
        alice.Drives.Social  = 0.70f;    // above SocialTriggerThreshold → context modifier inactive

        sim.Step();

        // Latrine is at (15,15) — alice should be heading there now
        Assert.NotNull(alice.Destination);
        Assert.Equal(15f, alice.Destination!.Value.X, precision: 1);
        Assert.Equal(15f, alice.Destination!.Value.Y, precision: 1);
        Assert.True(alice.NavState == NavigationState.Preempted ||
                    alice.NavState == NavigationState.Committed,
            $"Expected Preempted or Committed, got {alice.NavState}");
    }

    // ── AC10: Personal space — Bob stops at PersonalSpaceRadius from Alice ────

    [Fact]
    public void Step_SeekingAgent_StopsAtPersonalSpaceRadius()
    {
        const float PersonalSpaceRadius = 1.0f;
        var sim   = MakeSim();
        var alice = sim.GetAgent("alice")!;
        var bob   = sim.GetAgent("bob")!;

        // Alice is stationary (Idle)
        alice.Position = (5f, 5f);
        alice.NavState = NavigationState.Idle;
        alice.Drives.Social = 0.10f;   // all drives low — alice stays Idle

        // Bob is Seeking alice, starting 5 units away
        bob.Position     = (0f, 5f);
        bob.NavState     = NavigationState.Seeking;
        bob.SeekTargetId = "alice";
        bob.Destination  = alice.Position;
        bob.Drives.Social = 0.10f;   // bob's drives are low so decision maker returns wander
        // Override: force bob to keep seeking by setting social high only if needed.
        // Since NavState=Seeking and we call ResolveMovement in movement phase,
        // bob moves toward alice regardless of decision output.

        // Run enough steps for bob to approach alice and interact (SocialRange=2.5f)
        for (var i = 0; i < 10; i++)
        {
            var dist = PositionSystem.Distance(bob.Position, alice.Position);
            if (bob.NavState != NavigationState.Seeking) break;
            Assert.True(dist >= PersonalSpaceRadius,
                $"Step {i}: distance {dist:F3} < PersonalSpaceRadius {PersonalSpaceRadius}");
            sim.Step();
        }

        // After all steps: bob should not have gotten closer than PersonalSpaceRadius
        var finalDist = PositionSystem.Distance(bob.Position, alice.Position);
        Assert.True(finalDist >= PersonalSpaceRadius,
            $"Final distance {finalDist:F3} < PersonalSpaceRadius {PersonalSpaceRadius}");
    }

    // ── AC11: Social — within SocialRange, interaction fires ─────────────────

    [Fact]
    public void Step_WhenWithinSocialRange_InteractionFiresAndSocialDecreases()
    {
        var sim   = MakeSim();
        var bob   = sim.GetAgent("bob")!;
        var alice = sim.GetAgent("alice")!;

        // Place bob within SocialRange of alice
        alice.Position = (5f, 5f);
        alice.NavState = NavigationState.Idle;
        alice.Drives.Social = 0.10f;

        bob.Position     = (3f, 5f);   // distance from alice = 2.0f < SocialRange(2.5f)
        bob.NavState     = NavigationState.Seeking;
        bob.SeekTargetId = "alice";
        bob.Destination  = alice.Position;
        bob.Drives.Social  = 0.80f;   // high social drive — decision maker picks socialize

        var socialBefore = bob.Drives.Social;

        sim.Step();   // tick N: interaction fires, HadSocialInteractionLastTick set

        // Social satisfaction is applied on tick N+1 (same lag as Phase 2)
        sim.Step();   // tick N+1: DriveSystem applies satisfaction

        Assert.True(bob.Drives.Social < socialBefore,
            $"Expected social < {socialBefore:F2} after interaction, got {bob.Drives.Social:F2}");
    }

    // ── AC12: No partner available — all agents non-Idle, social decays ───────

    [Fact]
    public void Step_WhenNoIdlePartner_AgentDoesNotBeginSeeking()
    {
        var sim     = MakeSim();
        var alice   = sim.GetAgent("alice")!;
        var bob     = sim.GetAgent("bob")!;
        var charlie = sim.GetAgent("charlie")!;

        // Charlie wants to socialize
        charlie.Drives.Social = 0.80f;
        charlie.Drives.Hunger  = 0.10f;
        charlie.Drives.Thirst  = 0.10f;
        charlie.Drives.Fatigue = 0.10f;
        charlie.Drives.Bladder = 0.10f;
        charlie.NavState = NavigationState.Idle;

        // Alice and Bob are busy (non-Idle)
        alice.NavState = NavigationState.Committed;
        alice.Destination = (5f, 5f);

        bob.NavState = NavigationState.Seeking;
        bob.Destination = (10f, 10f);

        sim.Step();

        // Charlie should remain Idle — no partner available to seek
        Assert.Equal(NavigationState.Idle, charlie.NavState);
        Assert.Null(charlie.Destination);
    }
}
