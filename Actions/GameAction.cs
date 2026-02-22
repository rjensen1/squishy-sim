namespace SquishySim.Actions;

public record ActionEffect(
    float HungerDelta,
    float ThirstDelta,
    float FatigueDelta,
    float BladderDelta,
    float MoodDelta
);

public class GameAction
{
    public string Id          { get; init; } = "";
    public string Description { get; init; } = "";
    public ActionEffect Effect { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>Human-readable summary of what this action does to drives.</summary>
    public string EffectSummary
    {
        get
        {
            var parts = new List<string>();
            if (Effect.HungerDelta  != 0) parts.Add($"hunger {Effect.HungerDelta:+0.0;-0.0}");
            if (Effect.ThirstDelta  != 0) parts.Add($"thirst {Effect.ThirstDelta:+0.0;-0.0}");
            if (Effect.FatigueDelta != 0) parts.Add($"fatigue {Effect.FatigueDelta:+0.0;-0.0}");
            if (Effect.BladderDelta != 0) parts.Add($"bladder {Effect.BladderDelta:+0.0;-0.0}");
            if (Effect.MoodDelta    != 0) parts.Add($"mood {Effect.MoodDelta:+0.0;-0.0}");
            return string.Join(", ", parts);
        }
    }
}
