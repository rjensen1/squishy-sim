namespace SquishySim.Body;

/// <summary>
/// Accumulates biological drives each tick.
/// Rates are tuned so the creature needs to act every ~10-20 ticks to stay comfortable.
/// </summary>
public static class DriveSystem
{
    private const float HungerRate  = 0.012f;  // critical in ~83 ticks (reduced from 0.018 to account for Phase 3.5 travel time)
    private const float ThirstRate  = 0.015f;  // critical in ~67 ticks (reduced from 0.025 — was 2x other drives, causing constant hopping)
    private const float FatigueRate = 0.012f;  // critical in ~83 ticks

    // Social drive constants
    private const float SocialDecayRate    = 0.012f;  // isolation grows ~83 ticks to critical
    private const float SatisfactionAmount = 0.25f;   // qualifying interaction reduces isolation
    private const float MoodThreshold      = 0.70f;   // social isolation above this hurts mood
    private const float MoodDistressCoeff  = 0.12f;

    // SuppressionBudget constants — tuned empirically after gate is in (Phase 4 calibration).
    // Target: snaps under ~15–20 ticks of compound pressure, recovers in ~30–40 comfortable ticks.
    private const float SnapDepletionFast = 0.05f;   // compound pressure (hunger+fatigue >= 0.70): ~20 ticks to deplete
    private const float SnapDepletionSlow = 0.02f;   // single critical drive > 0.85f: ~50 ticks to deplete
    private const float SnapRegenRate     = 0.025f;  // all drives below 0.40f: ~40 ticks to recover

    /// <param name="hadQualifyingInteraction">
    /// True if this agent had a successful social interaction last tick (set by SimulationService).
    /// Satisfies the social drive instead of applying decay — mutually exclusive per tick.
    /// </param>
    public static void Tick(BodyState state, bool hadQualifyingInteraction = false)
    {
        var modifiers = DriveInteractionSystem.ComputeCrossEffects(state);

        state.Hunger  += HungerRate  * modifiers.HungerRateMultiplier;
        state.Thirst  += ThirstRate;
        state.Fatigue += FatigueRate * modifiers.FatigueRateMultiplier;

        // Social drive: decay OR satisfaction each tick (mutually exclusive)
        if (hadQualifyingInteraction)
            state.Social -= SatisfactionAmount;
        else
            state.Social += SocialDecayRate;

        // Mood degrades when critical drives go unmet
        float distress = 0f;
        if (state.Hunger  > 0.70f) distress += (state.Hunger  - 0.70f) * 0.10f;
        if (state.Thirst  > 0.70f) distress += (state.Thirst  - 0.70f) * 0.15f;
        if (state.Fatigue > 0.80f) distress += (state.Fatigue - 0.80f) * 0.08f;
        if (state.Bladder > 0.80f) distress += (state.Bladder - 0.80f) * 0.20f;
        if (state.Social >= MoodThreshold) distress += (state.Social - MoodThreshold) * MoodDistressCoeff;

        state.Mood -= distress;

        // SuppressionBudget: depletes under drive pressure, regenerates when comfortable.
        // Compound pressure takes priority; single critical is secondary; regen only when all drives low.
        bool compoundPressure = state.Hunger >= 0.70f && state.Fatigue >= 0.70f;
        bool singleCritical   = state.Hunger  > 0.85f || state.Thirst  > 0.85f ||
                                 state.Fatigue > 0.85f || state.Bladder > 0.85f;
        bool comfortable      = state.Hunger  < 0.40f && state.Thirst  < 0.40f &&
                                 state.Fatigue < 0.40f && state.Bladder < 0.40f;

        if (compoundPressure)
            state.SuppressionBudget -= SnapDepletionFast;
        else if (singleCritical)
            state.SuppressionBudget -= SnapDepletionSlow;
        else if (comfortable)
            state.SuppressionBudget += SnapRegenRate;

        // SnappedAt: set on first snap, cleared when budget meaningfully recovers above 0.25f.
        // Clamp() floors budget at 0f — evaluate after depletion but before clamp for correct transition detection.
        if (state.SuppressionBudget <= 0f && state.SnappedAt == null)
            state.SnappedAt = DateTime.UtcNow;
        else if (state.SuppressionBudget > 0.25f && state.SnappedAt != null)
            state.SnappedAt = null;

        state.Clamp();
    }
}
