namespace SquishySim.Actions;

public record ActionEffect(
    float HungerDelta,
    float ThirstDelta,
    float FatigueDelta,
    float BladderDelta,
    float MoodDelta,
    float SocialDelta = 0f
);

public class GameAction
{
    public string Id          { get; init; } = "";
    public string Description { get; init; } = "";
    public ActionEffect Effect { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>Action needs a Target — e.g. "talk" needs someone to talk to.</summary>
    public bool RequiresTarget  { get; init; } = false;

    /// <summary>Action needs spoken Content — e.g. "talk" needs the words said out loud.</summary>
    public bool RequiresContent { get; init; } = false;

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
            if (Effect.SocialDelta  != 0) parts.Add($"social {Effect.SocialDelta:+0.0;-0.0}");
            return string.Join(", ", parts);
        }
    }
}
