namespace SquishySim.Body;

/// <summary>
/// Accumulates biological drives each tick.
/// Rates are tuned so the creature needs to act every ~10-20 ticks to stay comfortable.
/// </summary>
public static class DriveSystem
{
    private const float HungerRate  = 0.018f;  // critical in ~56 ticks
    private const float ThirstRate  = 0.025f;  // critical in ~40 ticks
    private const float FatigueRate = 0.012f;  // critical in ~83 ticks

    public static void Tick(BodyState state)
    {
        state.Hunger  += HungerRate;
        state.Thirst  += ThirstRate;
        state.Fatigue += FatigueRate;

        // Mood degrades when critical drives go unmet
        float distress = 0f;
        if (state.Hunger  > 0.70f) distress += (state.Hunger  - 0.70f) * 0.10f;
        if (state.Thirst  > 0.70f) distress += (state.Thirst  - 0.70f) * 0.15f;
        if (state.Fatigue > 0.80f) distress += (state.Fatigue - 0.80f) * 0.08f;
        if (state.Bladder > 0.80f) distress += (state.Bladder - 0.80f) * 0.20f;

        state.Mood -= distress;

        state.Clamp();
    }
}
