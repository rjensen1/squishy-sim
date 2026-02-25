namespace SquishySim.Services;

/// <summary>
/// Pure spatial math helpers — no simulation state, fully unit-testable.
/// </summary>
public static class PositionSystem
{
    /// <summary>
    /// Move <paramref name="position"/> toward <paramref name="destination"/> by
    /// <paramref name="speed"/> units. Snaps to destination if closer than speed.
    /// </summary>
    public static (float X, float Y) ComputeNewPosition(
        (float X, float Y) position,
        (float X, float Y) destination,
        float speed)
    {
        var dx = destination.X - position.X;
        var dy = destination.Y - position.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance <= speed || distance == 0f)
            return destination;

        var scale = speed / distance;
        return (position.X + dx * scale, position.Y + dy * scale);
    }

    /// <summary>
    /// Returns true when <paramref name="position"/> is within <paramref name="radius"/>
    /// of <paramref name="target"/> (inclusive boundary: distance &lt;= radius).
    /// </summary>
    public static bool HasArrived(
        (float X, float Y) position,
        (float X, float Y) target,
        float radius)
    {
        return Distance(position, target) <= radius;
    }

    public static float Distance((float X, float Y) a, (float X, float Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
