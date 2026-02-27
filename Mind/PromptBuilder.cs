using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

public static class PromptBuilder
{
    public static string Build(
        BodyState state,
        IReadOnlyList<GameAction> actions,
        string? persona = null,
        string? navState = null)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You control a simulated creature. Choose the best action given its current biological state.");

        if (!string.IsNullOrEmpty(persona))
        {
            sb.AppendLine();
            sb.AppendLine($"Your disposition: {persona}");
        }

        sb.AppendLine();
        sb.AppendLine("Current state (0.0 = satisfied, 1.0 = critical):");
        sb.AppendLine($"  hunger:   {state.Hunger:0.00}  ({state.HungerLabel})");
        sb.AppendLine($"  thirst:   {state.Thirst:0.00}  ({state.ThirstLabel})");
        sb.AppendLine($"  fatigue:  {state.Fatigue:0.00}  ({state.FatigueLabel})");
        sb.AppendLine($"  bladder:  {state.Bladder:0.00}  ({state.BladderLabel})");
        sb.AppendLine($"  mood:     {state.Mood:0.00}  ({state.MoodLabel}) [higher is better]");
        sb.AppendLine($"  social:   {state.Social:0.00}  (isolation; higher = more isolated)");
        sb.AppendLine($"  budget:   {state.SuppressionBudget:0.00}  (suppression capacity; lower = less able to delay needs)");

        if (state.SnappedAt.HasValue)
            sb.AppendLine("  ** SNAPPED: suppression capacity exhausted. Act on your needs directly. **");

        if (!string.IsNullOrEmpty(navState))
            sb.AppendLine($"  movement: {navState}");

        sb.AppendLine();
        sb.AppendLine("Available actions and their effects:");
        foreach (var action in actions)
            sb.AppendLine($"  {action.Id}: {action.EffectSummary}");

        sb.AppendLine();
        sb.AppendLine("Respond with ONLY a single line of valid JSON. No markdown, no explanation:");
        sb.Append("{\"action\": \"<action_id>\", \"reason\": \"<one short sentence>\"}");

        return sb.ToString();
    }
}
