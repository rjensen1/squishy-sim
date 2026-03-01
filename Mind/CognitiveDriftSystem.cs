using SquishySim.Domain;

namespace SquishySim.Mind;

/// <summary>
/// Accumulates cognitive drift in agents under sustained social isolation.
/// PersonaDriftFactor represents divergence from the agent's base persona:
///   0.0 = fully aligned with base persona
///   1.0 = fully drifted — identity feels inaccessible
///
/// Cognitive drift has a slower time constant than behavioral coherence degradation.
/// An agent may lose behavioral decisiveness before (or after) their inner voice shifts —
/// both layers are independently testable.
/// </summary>
public static class CognitiveDriftSystem
{
    private const float DriftAccumulationRate = 0.005f;  // ~200 ticks isolated → full drift
    private const float DriftRecoveryRate     = 0.008f;  // ~125 ticks social → clear drift

    private const float IsolationThreshold = 0.50f;  // above this: drift accumulates
    private const float RecoveryThreshold  = 0.30f;  // below this: drift recovers

    public static void Tick(Agent agent)
    {
        if (agent.Drives.Social > IsolationThreshold)
            agent.PersonaDriftFactor = Math.Min(1.0f, agent.PersonaDriftFactor + DriftAccumulationRate);
        else if (agent.Drives.Social < RecoveryThreshold)
            agent.PersonaDriftFactor = Math.Max(0.0f, agent.PersonaDriftFactor - DriftRecoveryRate);
        // Between thresholds: no change — plateaus at current drift level
    }

    /// <summary>
    /// Returns a persona string modified to reflect cognitive drift state.
    /// </summary>
    public static string? BuildDriftedPersona(string? basePersona, float driftFactor)
    {
        if (string.IsNullOrEmpty(basePersona) || driftFactor < 0.10f)
            return basePersona;

        if (driftFactor < 0.40f)
            return basePersona + " Lately you find it harder to concentrate — things that used to matter feel distant.";

        if (driftFactor < 0.70f)
            return basePersona + " You feel disconnected from yourself. Your usual instincts feel muffled.";

        return basePersona + " The person you were is hard to locate. Your priorities feel interchangeable.";
    }
}
