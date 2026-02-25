namespace SquishySim.Body;

/// <summary>
/// Computes cross-drive decay rate modifiers from a snapshot of drive state.
/// Pure function — no side effects, no time or RNG dependencies.
/// Called from DriveSystem.Tick() before accumulation.
/// </summary>
public static class DriveInteractionSystem
{
    // Thresholds at which cross-effects activate (flat/binary — full modifier applied on crossing)
    private const float HungerToFatigueThreshold = 0.60f;
    private const float FatigueToHungerThreshold = 0.60f;
    private const float CompoundThreshold         = 0.70f;

    // Multipliers applied to the affected drive's accumulation rate
    private const float HungerToFatigueMultiplier = 1.20f;
    private const float FatigueToHungerMultiplier = 1.25f;
    private const float CompoundMultiplier         = 1.40f;

    /// <summary>
    /// Returns rate multipliers for this tick based on current drive values.
    /// Uses snapshot semantics: all rules read from the same input state.
    /// </summary>
    public static DriveDecayModifiers ComputeCrossEffects(BodyState snapshot)
    {
        // Compound rule: both hunger AND fatigue above compound threshold
        // replaces (not stacks) the individual modifiers
        if (snapshot.Hunger >= CompoundThreshold && snapshot.Fatigue >= CompoundThreshold)
            return new DriveDecayModifiers(
                HungerRateMultiplier:  CompoundMultiplier,
                FatigueRateMultiplier: CompoundMultiplier);

        float hungerRateMult  = snapshot.Fatigue >= FatigueToHungerThreshold
            ? FatigueToHungerMultiplier : 1.0f;

        float fatigueRateMult = snapshot.Hunger >= HungerToFatigueThreshold
            ? HungerToFatigueMultiplier : 1.0f;

        return new DriveDecayModifiers(
            HungerRateMultiplier:  hungerRateMult,
            FatigueRateMultiplier: fatigueRateMult);
    }
}
