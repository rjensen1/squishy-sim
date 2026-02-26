namespace SquishySim.Domain;

public enum NavigationState
{
    Idle,         // Stationary — no current destination
    Committed,    // Moving toward a resource destination
    Preempted,    // Higher-priority drive rerouted mid-navigation
    Seeking,      // Moving toward another agent to satisfy social drive
    // Opportunistic — Phase 4 (deferred)
}
