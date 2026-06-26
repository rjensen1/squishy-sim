using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

public static class PromptBuilder
{
    public static string Build(
        BodyState state,
        IReadOnlyList<GameAction> actions,
        string? persona = null,
        string? navState = null,
        IReadOnlyList<string>? targets = null)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You ARE this character — a person living a life, not just a body. Choose the single best action given everything below.");
        sb.AppendLine("Tend your physical needs, but company matters too: when you feel isolated (a high social value), talking to someone is often the right move, not just eating or drinking.");

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

        // List who can be addressed, but only if a parameterized action is on the menu.
        var hasTargetedAction = actions.Any(a => a.RequiresTarget);
        if (hasTargetedAction && targets is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"People you can talk to (use one of these exact names as \"target\"): {string.Join(", ", targets)}");
        }

        sb.AppendLine();
        sb.AppendLine("Respond with ONLY a single line of valid JSON. No markdown, no explanation:");
        sb.AppendLine("{\"action\": \"<action_id>\", \"reason\": \"<one short sentence>\"}");
        if (hasTargetedAction)
        {
            sb.Append("For \"talk\", also include who you address and the exact words you say out loud: ");
            sb.Append("{\"action\": \"talk\", \"target\": \"<name>\", \"content\": \"<what you say>\", \"reason\": \"<why>\"}");
        }

        return sb.ToString();
    }
}
