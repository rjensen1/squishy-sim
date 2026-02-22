namespace SquishySim.Body;

/// <summary>
/// Biological drive state for a simulated creature.
/// All values are floats in [0.0, 1.0] where 1.0 = fully deprived / critical.
/// Mood is inverted: 1.0 = fully happy.
/// </summary>
public class BodyState
{
    public float Hunger  { get; set; } = 0.10f;
    public float Thirst  { get; set; } = 0.10f;
    public float Fatigue { get; set; } = 0.10f;
    public float Bladder { get; set; } = 0.00f;
    public float Mood    { get; set; } = 0.70f;

    public string HungerLabel  => DriveLabel(Hunger);
    public string ThirstLabel  => DriveLabel(Thirst);
    public string FatigueLabel => DriveLabel(Fatigue);
    public string BladderLabel => DriveLabel(Bladder);

    public string MoodLabel => Mood switch
    {
        > 0.75f => "good",
        > 0.50f => "okay",
        > 0.30f => "poor",
        _       => "BAD"
    };

    private static string DriveLabel(float v) => v switch
    {
        > 0.85f => "CRITICAL",
        > 0.65f => "urgent",
        > 0.40f => "moderate",
        _       => "low"
    };

    public void Clamp()
    {
        Hunger  = Math.Clamp(Hunger,  0f, 1f);
        Thirst  = Math.Clamp(Thirst,  0f, 1f);
        Fatigue = Math.Clamp(Fatigue, 0f, 1f);
        Bladder = Math.Clamp(Bladder, 0f, 1f);
        Mood    = Math.Clamp(Mood,    0f, 1f);
    }
}
