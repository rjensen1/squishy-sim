namespace SquishySim.Body;

/// <summary>
/// Per-tick rate multipliers produced by DriveInteractionSystem.
/// All multipliers default to 1.0 (no effect). MoodDecayMultiplier and MoodFlatPenalty
/// are reserved for future phases and unused in Phase 1.
/// </summary>
public record DriveDecayModifiers(
    float HungerRateMultiplier  = 1.0f,
    float FatigueRateMultiplier = 1.0f,
    float MoodDecayMultiplier   = 1.0f,
    float MoodFlatPenalty       = 0.0f
);
