// PROTOTYPE: In-memory simulation service — state is lost on restart
using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Domain;
using SquishySim.Mind;

namespace SquishySim.Services;

public class SimulationService
{
    private readonly List<Agent> _agents = new();
    private readonly List<ConversationMessage> _allConversations = new();
    private readonly object _lock = new();
    private readonly Random _rng = new();

    private bool   _isPaused        = false;
    private int    _tickCount       = 0;
    private double _speedMultiplier = 1.0;
    private Timer? _autoTimer;

    // Tick interval base = 2 seconds at speed 1.0
    private const double BaseTickMs = 2000;

    // ── Spatial constants ─────────────────────────────────────────────────────

    private const float WorldSize              = 20.0f;
    private const float MoveSpeed             = 1.0f;
    private const float ResourceInteractRadius = 1.5f;
    private const float SocialRange            = 2.5f;
    private const float PersonalSpaceRadius    = 1.0f;

    // Fixed resource positions mapped by action ID
    private static readonly Dictionary<string, ResourceLocation> ResourcesByAction = new()
    {
        ["eat_food"]    = new ResourceLocation("Food station",  ( 5f,  5f), "eat_food"),
        ["drink_water"] = new ResourceLocation("Water source",  (15f,  5f), "drink_water"),
        ["use_toilet"]  = new ResourceLocation("Latrine",       (15f, 15f), "use_toilet"),
        ["sleep"]       = new ResourceLocation("Shelter",       ( 5f, 15f), "sleep"),
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public SimulationService()
    {
        // PROTOTYPE: Seed three agents with rule-based decision makers at spread starting positions
        var seeds = new[]
        {
            ("alice",   "Alice",   ( 3f, 10f)),
            ("bob",     "Bob",     (10f,  3f)),
            ("charlie", "Charlie", (17f, 10f)),
        };

        foreach (var (id, name, pos) in seeds)
        {
            _agents.Add(new Agent
            {
                Id            = id,
                Name          = name,
                Position      = pos,
                DecisionMaker = new RuleBasedDecisionMaker()
            });
        }
    }

    // ── Public state accessors ────────────────────────────────────────────────

    public bool   IsPaused        { get { lock (_lock) return _isPaused; } }
    public int    TickCount       { get { lock (_lock) return _tickCount; } }
    public double SpeedMultiplier { get { lock (_lock) return _speedMultiplier; } }

    public IReadOnlyList<Agent> Agents
    {
        get { lock (_lock) return _agents.ToList(); }
    }

    public Agent? GetAgent(string id)
    {
        lock (_lock) return _agents.FirstOrDefault(a => a.Id == id);
    }

    public IReadOnlyList<ConversationMessage> AllConversations
    {
        get { lock (_lock) return _allConversations.ToList(); }
    }

    // ── Simulation control ────────────────────────────────────────────────────

    public void Pause()
    {
        lock (_lock)
        {
            _isPaused = true;
            _autoTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            _isPaused = false;
            RestartTimer();
        }
    }

    public void SetSpeed(double multiplier)
    {
        lock (_lock)
        {
            _speedMultiplier = Math.Clamp(multiplier, 0.25, 4.0);
            if (!_isPaused) RestartTimer();
        }
    }

    // Explicit step — always advances one tick regardless of pause state
    public void Step()
    {
        lock (_lock) AdvanceTick();
    }

    public void StartAutoAdvance()
    {
        lock (_lock)
        {
            if (!_isPaused) RestartTimer();
        }
    }

    // ── Drive injection ───────────────────────────────────────────────────────

    public bool SetDrive(string agentId, string drive, double value)
    {
        lock (_lock)
        {
            var agent = _agents.FirstOrDefault(a => a.Id == agentId);
            if (agent == null) return false;

            var clamped = (float)Math.Clamp(value, 0.0, 1.0);
            switch (drive.ToLowerInvariant())
            {
                case "hunger":  agent.Drives.Hunger  = clamped; break;
                case "thirst":  agent.Drives.Thirst  = clamped; break;
                case "fatigue": agent.Drives.Fatigue = clamped; break;
                case "bladder": agent.Drives.Bladder = clamped; break;
                case "social":  agent.Drives.Social  = clamped; break;
                case "mood":    agent.Drives.Mood    = clamped; break;
                default: return false;
            }
            return true;
        }
    }

    // ── LLM config ────────────────────────────────────────────────────────────

    public bool SetLlmConfig(string agentId, string model, string baseUrl, string? apiKey)
    {
        lock (_lock)
        {
            var agent = _agents.FirstOrDefault(a => a.Id == agentId);
            if (agent == null) return false;

            agent.LlmConfig.Model   = model;
            agent.LlmConfig.BaseUrl = baseUrl;
            if (apiKey != null) agent.LlmConfig.SetApiKey(apiKey);

            // PROTOTYPE: Only Ollama and rule-based supported
            agent.DecisionMaker = model == "rule-based"
                ? new RuleBasedDecisionMaker()
                : new OllamaDecisionMaker(model, baseUrl);

            return true;
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void RestartTimer()
    {
        var intervalMs = (int)(BaseTickMs / _speedMultiplier);
        _autoTimer ??= new Timer(_ => { lock (_lock) { if (!_isPaused) AdvanceTick(); } });
        _autoTimer.Change(intervalMs, intervalMs);
    }

    private void AdvanceTick()
    {
        _tickCount++;
        var now = DateTimeOffset.UtcNow;

        // ── Phase 1: Consume prior-tick social interaction flags ───────────────
        var hadInteractionLastTick = _agents
            .Where(a => a.HadSocialInteractionLastTick)
            .Select(a => a.Id)
            .ToHashSet();
        foreach (var a in _agents) a.HadSocialInteractionLastTick = false;

        // ── Phase 2: Drive tick ───────────────────────────────────────────────
        foreach (var agent in _agents)
            DriveSystem.Tick(agent.Drives, hadInteractionLastTick.Contains(agent.Id));

        // Snapshot which agents were Idle before decision-making runs.
        // Agents transition to Committed(wander) during the decision phase, which would
        // incorrectly mark them as unavailable for seeking within the same tick.
        var idleAtTickStart = _agents
            .Where(a => a.NavState == NavigationState.Idle)
            .Select(a => a.Id)
            .ToHashSet();

        // ── Phase 3: Decision making → navigation setup ───────────────────────
        foreach (var agent in _agents)
        {
            try
            {
                // PROTOTYPE: .GetResult() holds _lock for the LLM HTTP call duration.
                // Acceptable for rule-based; Ollama will block. See existing prototype note.
                var (action, reason) = agent.DecisionMaker
                    .ChooseAsync(agent.Drives, ActionCatalog.All)
                    .GetAwaiter().GetResult();

                agent.CurrentAction = action.Id;
                agent.CurrentReason = reason;

                UpdateNavigation(agent, action, idleAtTickStart);

                agent.Thoughts.Add(new ThoughtEntry(now,
                    $"{agent.CurrentAction}: {agent.CurrentReason} | nav={agent.NavState}"));
                if (agent.Thoughts.Count > 200) agent.Thoughts.RemoveAt(0);
            }
            catch (Exception ex)
            {
                agent.Thoughts.Add(new ThoughtEntry(now, $"[error] {ex.Message}"));
            }
        }

        // ── Phase 4: Movement resolution ──────────────────────────────────────
        foreach (var agent in _agents)
            ResolveMovement(agent, now, idleAtTickStart);

        // ── Phase 5: Background chatter ───────────────────────────────────────
        MaybeGenerateConversations(now);
    }

    /// <summary>
    /// Maps the chosen action to a navigation destination (or updates seek state).
    /// Does NOT apply drive effects — those are applied on arrival.
    /// </summary>
    private void UpdateNavigation(Agent agent, GameAction action, HashSet<string> idleAtTickStart)
    {
        if (action.Id == "socialize")
        {
            BeginOrContinueSeeking(agent, idleAtTickStart);
            return;
        }

        if (ResourcesByAction.TryGetValue(action.Id, out var resource))
        {
            var dest = resource.Position;

            if (agent.NavState == NavigationState.Idle)
            {
                // Fresh start
                agent.Destination = dest;
                agent.NavState    = NavigationState.Committed;
            }
            else if (agent.NavState == NavigationState.Seeking)
            {
                // Physical drive overrides seeking — preempt
                agent.Destination   = dest;
                agent.NavState      = NavigationState.Preempted;
                agent.SeekTargetId  = null;
            }
            else if (agent.Destination.HasValue &&
                     PositionSystem.Distance(agent.Destination.Value, dest) > 0.01f)
            {
                // Different resource — preempt current nav
                agent.Destination = dest;
                agent.NavState    = NavigationState.Preempted;
            }
            // else: already heading to the same resource, no change
            return;
        }

        // "wander" or unmapped action: set a random destination if idle
        if (action.Id == "wander" && agent.NavState == NavigationState.Idle)
        {
            agent.Destination = ((float)(_rng.NextDouble() * WorldSize),
                                 (float)(_rng.NextDouble() * WorldSize));
            agent.NavState    = NavigationState.Committed;
        }
    }

    private void BeginOrContinueSeeking(Agent agent, HashSet<string> idleAtTickStart)
    {
        if (agent.NavState == NavigationState.Seeking)
            return; // already seeking — movement phase handles it each tick

        // Use tick-start snapshot: an agent that transitioned to Committed(wander)
        // during this tick's decision phase is still logically available.
        var target = _agents
            .Where(a => a.Id != agent.Id && idleAtTickStart.Contains(a.Id))
            .MinBy(a => PositionSystem.Distance(agent.Position, a.Position));

        if (target == null)
        {
            // No available partner
            agent.CurrentReason = $"{agent.CurrentReason} (no partner available)";
            return;
        }

        agent.SeekTargetId = target.Id;
        agent.Destination  = target.Position;
        agent.NavState     = NavigationState.Seeking;
    }

    private void ResolveMovement(Agent agent, DateTimeOffset now, HashSet<string> idleAtTickStart)
    {
        if (agent.Destination == null || agent.NavState == NavigationState.Idle)
            return;

        if (agent.NavState == NavigationState.Seeking)
        {
            ResolveSeekMovement(agent, now, idleAtTickStart);
        }
        else
        {
            ResolveResourceMovement(agent);
        }
    }

    private void ResolveSeekMovement(Agent agent, DateTimeOffset now, HashSet<string> idleAtTickStart)
    {
        // Re-resolve target (uses tick-start snapshot for availability)
        var target = ResolveSeekTarget(agent, idleAtTickStart);
        if (target == null)
        {
            agent.NavState     = NavigationState.Idle;
            agent.Destination  = null;
            agent.SeekTargetId = null;
            return;
        }

        // Use the destination set during the decision phase (target's position at tick start)
        // for the SocialRange check. Agents process movement sequentially — using the stored
        // destination prevents "target moved in Phase 4 before my turn" false misses.
        var dest = agent.Destination!.Value;
        var dist = PositionSystem.Distance(agent.Position, dest);

        if (dist <= SocialRange)
        {
            // Within social range — interaction fires this tick
            agent.HadSocialInteractionLastTick  = true;
            target.HadSocialInteractionLastTick = true;

            var text = $"Hey {target.Name}, wanted to catch up.";
            var msg  = new ConversationMessage(now, agent.Id, target.Id, text);
            _allConversations.Add(msg);
            agent.Messages.Add(msg);
            target.Messages.Add(msg);
            if (_allConversations.Count > 500) _allConversations.RemoveAt(0);

            agent.CurrentAction = "socialize";
            agent.CurrentReason = $"with {target.Name}";

            agent.NavState     = NavigationState.Idle;
            agent.Destination  = null;
            agent.SeekTargetId = null;
        }
        else
        {
            // Update destination to target's current position before moving
            agent.Destination = target.Position;
            dest = agent.Destination.Value;

            // Move toward target — stop at PersonalSpaceRadius
            var maxMovement = dist - PersonalSpaceRadius;
            if (maxMovement > 0f)
            {
                var movement = Math.Min(MoveSpeed, maxMovement);
                var dx = (dest.X - agent.Position.X) / dist;
                var dy = (dest.Y - agent.Position.Y) / dist;
                agent.Position = (agent.Position.X + dx * movement,
                                  agent.Position.Y + dy * movement);
            }
        }
    }

    private Agent? ResolveSeekTarget(Agent agent, HashSet<string> idleAtTickStart)
    {
        // Prefer previously committed target if it was Idle at tick start
        if (agent.SeekTargetId != null)
        {
            var stored = _agents.FirstOrDefault(
                a => a.Id == agent.SeekTargetId && idleAtTickStart.Contains(a.Id));
            if (stored != null) return stored;
        }

        // Re-find nearest agent that was Idle at tick start
        var nearest = _agents
            .Where(a => a.Id != agent.Id && idleAtTickStart.Contains(a.Id))
            .MinBy(a => PositionSystem.Distance(agent.Position, a.Position));

        if (nearest != null) agent.SeekTargetId = nearest.Id;
        return nearest;
    }

    private void ResolveResourceMovement(Agent agent)
    {
        var dest = agent.Destination!.Value;

        if (PositionSystem.HasArrived(agent.Position, dest, ResourceInteractRadius))
        {
            // Apply drive effects on arrival
            ApplyArrivalEffects(agent);
            agent.NavState    = NavigationState.Idle;
            agent.Destination = null;
        }
        else
        {
            agent.Position = PositionSystem.ComputeNewPosition(agent.Position, dest, MoveSpeed);
            // After first tick of Preempted, transition to Committed (en route to new destination)
            if (agent.NavState == NavigationState.Preempted)
                agent.NavState = NavigationState.Committed;
        }
    }

    private static void ApplyArrivalEffects(Agent agent)
    {
        var action = ActionCatalog.FindById(agent.CurrentAction);
        if (action == null) return;

        agent.Drives.Hunger  += action.Effect.HungerDelta;
        agent.Drives.Thirst  += action.Effect.ThirstDelta;
        agent.Drives.Fatigue += action.Effect.FatigueDelta;
        agent.Drives.Bladder += action.Effect.BladderDelta;
        agent.Drives.Mood    += action.Effect.MoodDelta;
        agent.Drives.Clamp();
    }

    private void MaybeGenerateConversations(DateTimeOffset now)
    {
        // PROTOTYPE: Every 5 ticks, the most distressed agent broadcasts their state.
        // Background chatter does NOT satisfy the social drive.
        if (_tickCount % 5 != 0 || _agents.Count < 2) return;

        var talker = _agents.MaxBy(a =>
            a.Drives.Hunger + a.Drives.Thirst + a.Drives.Fatigue + a.Drives.Bladder - a.Drives.Mood);
        if (talker == null) return;

        var listener = _agents.Where(a => a.Id != talker.Id)
            .OrderBy(_ => Guid.NewGuid()).First();

        var text = talker.CurrentAction switch
        {
            "eat_food"    => "Really needed that. Hunger was getting bad.",
            "drink_water" => "So thirsty. Drinking now.",
            "sleep"       => "Exhausted. Going to rest for a bit.",
            "use_toilet"  => "Had to go urgently.",
            "socialize"   => $"Good to talk. Social drive was high.",
            "wander"      => $"Just wandering around. Mood's {talker.Drives.MoodLabel}.",
            "do_nothing"  => "Not feeling great. Just sitting here.",
            _             => $"Doing {talker.CurrentAction}."
        };

        var msg = new ConversationMessage(now, talker.Id, listener.Id, text);
        _allConversations.Add(msg);
        talker.Messages.Add(msg);
        listener.Messages.Add(msg);

        if (_allConversations.Count > 500) _allConversations.RemoveAt(0);
    }
}
