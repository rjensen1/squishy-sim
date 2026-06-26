using SquishySim.Actions;

namespace SquishySim.Mind;

/// <summary>
/// The outcome of a decision maker choosing an action.
/// <para>
/// Target/Content are only used by parameterized verbs:
///   <c>Target</c>  — the named thing the action is aimed at (e.g. the agent to "talk" to)
///   <c>Content</c>  — the spoken line for a "talk" action (what the agent says out loud)
/// </para>
/// For drive actions (eat, sleep, …) both are null.
/// </summary>
public record DecisionResult(GameAction Action, string Reason, string? Target = null, string? Content = null)
{
    // Back-compat: existing call sites destructure the result as (action, reason).
    // Keeps every `var (action, reason) = await ChooseAsync(...)` compiling untouched.
    public void Deconstruct(out GameAction action, out string reason)
    {
        action = Action;
        reason = Reason;
    }
}
