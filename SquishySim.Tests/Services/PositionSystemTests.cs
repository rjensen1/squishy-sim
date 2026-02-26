using SquishySim.Services;

namespace SquishySim.Tests.Services;

public class PositionSystemTests
{
    // ── AC1: Moves by MoveSpeed toward destination ────────────────────────────

    [Fact]
    public void ComputeNewPosition_MovesUnitVectorTimesMoveSpeed()
    {
        // pos=(0,0), dest=(3,4) → distance=5.0 → unit=(0.6,0.8) → expected=(0.6,0.8)
        var result = PositionSystem.ComputeNewPosition((0f, 0f), (3f, 4f), speed: 1.0f);

        Assert.Equal(0.6f, result.X, precision: 5);
        Assert.Equal(0.8f, result.Y, precision: 5);
    }

    // ── AC2: Overshoot snaps to destination ──────────────────────────────────

    [Fact]
    public void ComputeNewPosition_SnapsToDest_WhenCloserThanSpeed()
    {
        // pos=(0,0), dest=(0.5,0), speed=1.0 → 0.5 < 1.0 → snap
        var result = PositionSystem.ComputeNewPosition((0f, 0f), (0.5f, 0f), speed: 1.0f);

        Assert.Equal(0.5f, result.X);
        Assert.Equal(0.0f, result.Y);
    }

    // ── AC3: Already at destination — no movement ─────────────────────────────

    [Fact]
    public void ComputeNewPosition_ReturnsDestination_WhenAlreadyThere()
    {
        var result = PositionSystem.ComputeNewPosition((5f, 5f), (5f, 5f), speed: 1.0f);

        Assert.Equal(5f, result.X);
        Assert.Equal(5f, result.Y);
    }

    // ── AC4: Not arrived — distance > radius ─────────────────────────────────

    [Fact]
    public void HasArrived_ReturnsFalse_WhenDistanceExceedsRadius()
    {
        // pos=(0,0), target=(3,0), radius=1.5 → distance=3.0 > 1.5 → false
        Assert.False(PositionSystem.HasArrived((0f, 0f), (3f, 0f), radius: 1.5f));
    }

    // ── AC5: Arrived — distance == radius (inclusive boundary) ───────────────

    [Fact]
    public void HasArrived_ReturnsTrue_WhenDistanceEqualsRadius()
    {
        // pos=(0,0), target=(1.5,0), radius=1.5 → distance=1.5 <= 1.5 → true
        Assert.True(PositionSystem.HasArrived((0f, 0f), (1.5f, 0f), radius: 1.5f));
    }

    // ── AC6: Arrived — distance < radius ─────────────────────────────────────

    [Fact]
    public void HasArrived_ReturnsTrue_WhenDistanceLessThanRadius()
    {
        // pos=(0,0), target=(1.0,0), radius=1.5 → distance=1.0 < 1.5 → true
        Assert.True(PositionSystem.HasArrived((0f, 0f), (1.0f, 0f), radius: 1.5f));
    }
}
