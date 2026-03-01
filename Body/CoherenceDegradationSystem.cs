namespace SquishySim.Body;

/// <summary>
/// Applies behavioral coherence degradation to drive snapshots under social isolation.
/// Isolation compresses physical drive urgency differentials toward their mean — the agent
/// can still feel all drives, but loses the ability to prioritize between them.
///
/// This is distinct from SuppressionBudget snap (crisis/overload). Snap = can't suppress any drive.
/// Coherence degradation = can't distinguish which drive is most urgent.
/// </summary>
public static class CoherenceDegradationSystem
{
    // Isolation below this threshold produces no compression.
    private const float IsolationOnsetThreshold = 0.3f;

    /// <summary>
    /// Returns a modified copy of <paramref name="snapshot"/> with physical drive urgencies
    /// compressed toward their mean, proportional to the isolation level.
    /// Social, Mood, and SuppressionBudget are not affected.
    /// </summary>
    public static BodyState ApplyToSnapshot(BodyState snapshot)
    {
        float isolationFactor = ComputeIsolationFactor(snapshot.Social);
        if (isolationFactor <= 0f) return snapshot;

        float mean = (snapshot.Hunger + snapshot.Thirst + snapshot.Fatigue + snapshot.Bladder) / 4f;

        snapshot.Hunger  = Lerp(snapshot.Hunger,  mean, isolationFactor);
        snapshot.Thirst  = Lerp(snapshot.Thirst,  mean, isolationFactor);
        snapshot.Fatigue = Lerp(snapshot.Fatigue, mean, isolationFactor);
        snapshot.Bladder = Lerp(snapshot.Bladder, mean, isolationFactor);

        return snapshot;
    }

    /// <summary>
    /// Behavioral coherence as a [0, 1] value: 1.0 = fully coherent, 0.0 = fully incoherent.
    /// Derived from current social isolation level.
    /// </summary>
    public static float BehavioralCoherence(float social) =>
        1.0f - ComputeIsolationFactor(social);

    internal static float ComputeIsolationFactor(float social) =>
        Math.Max(0f, social - IsolationOnsetThreshold) / (1.0f - IsolationOnsetThreshold);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
