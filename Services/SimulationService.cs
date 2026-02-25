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

    private bool   _isPaused         = false;
    private int    _tickCount        = 0;
    private double _speedMultiplier  = 1.0;
    private Timer? _autoTimer;

    // Tick interval base = 2 seconds at speed 1.0
    private const double BaseTickMs = 2000;

    public SimulationService()
    {
        // PROTOTYPE: Seed three agents with rule-based decision makers
        foreach (var (id, name) in new[] { ("alice", "Alice"), ("bob", "Bob"), ("charlie", "Charlie") })
        {
            _agents.Add(new Agent
            {
                Id   = id,
                Name = name,
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

            // Rebuild decision maker with new config
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

        // Collect which agents had a successful social interaction last tick,
        // then reset the flags so this tick starts fresh.
        var hadInteractionLastTick = _agents
            .Where(a => a.HadSocialInteractionLastTick)
            .Select(a => a.Id)
            .ToHashSet();
        foreach (var a in _agents) a.HadSocialInteractionLastTick = false;

        foreach (var agent in _agents)
        {
            DriveSystem.Tick(agent.Drives, hadInteractionLastTick.Contains(agent.Id));

            try
            {
                // PROTOTYPE: .GetResult() holds _lock for the full LLM HTTP call duration.
                // With rule-based this is fine. With Ollama, REST endpoints will block
                // while the lock is held — web UI freezes during each tick.
                // Before enabling Ollama properly: release lock, await async, re-acquire.
                var (action, reason) = agent.DecisionMaker
                    .ChooseAsync(agent.Drives, ActionCatalog.All)
                    .GetAwaiter().GetResult();

                if (action.Id == "socialize")
                {
                    ExecuteSocializeAction(agent, now, reason);
                }
                else
                {
                    agent.Drives.Hunger  += action.Effect.HungerDelta;
                    agent.Drives.Thirst  += action.Effect.ThirstDelta;
                    agent.Drives.Fatigue += action.Effect.FatigueDelta;
                    agent.Drives.Bladder += action.Effect.BladderDelta;
                    agent.Drives.Mood    += action.Effect.MoodDelta;
                    agent.Drives.Clamp();

                    agent.CurrentAction = action.Id;
                    agent.CurrentReason = reason;
                }

                agent.Thoughts.Add(new ThoughtEntry(now, $"{agent.CurrentAction}: {agent.CurrentReason}"));
                if (agent.Thoughts.Count > 200) agent.Thoughts.RemoveAt(0);
            }
            catch (Exception ex)
            {
                agent.Thoughts.Add(new ThoughtEntry(now, $"[error] {ex.Message}"));
            }
        }

        // PROTOTYPE: Simple inter-agent messaging — agents occasionally comment on state.
        // Background chatter does NOT satisfy the social drive (no HadSocialInteractionLastTick set).
        MaybeGenerateConversations(now);
    }

    private void ExecuteSocializeAction(Agent agent, DateTimeOffset now, string reason)
    {
        var partner = _agents
            .Where(a => a.Id != agent.Id)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault();

        if (partner == null)
        {
            // No partner available — action fails, social drive unchanged this tick
            agent.CurrentAction = "socialize";
            agent.CurrentReason = $"{reason} (no partner available)";
            return;
        }

        // Successful interaction — both agents receive social satisfaction next tick
        agent.HadSocialInteractionLastTick   = true;
        partner.HadSocialInteractionLastTick = true;

        agent.CurrentAction = "socialize";
        agent.CurrentReason = $"{reason} (with {partner.Name})";

        var text = $"Hey {partner.Name}, wanted to catch up.";
        var msg  = new ConversationMessage(now, agent.Id, partner.Id, text);
        _allConversations.Add(msg);
        agent.Messages.Add(msg);
        partner.Messages.Add(msg);

        if (_allConversations.Count > 500) _allConversations.RemoveAt(0);
    }

    private void MaybeGenerateConversations(DateTimeOffset now)
    {
        // PROTOTYPE: Every 5 ticks, the most distressed agent broadcasts their state
        if (_tickCount % 5 != 0 || _agents.Count < 2) return;

        var talker = _agents.MaxBy(a =>
            a.Drives.Hunger + a.Drives.Thirst + a.Drives.Fatigue + a.Drives.Bladder - a.Drives.Mood);
        if (talker == null) return;

        var listener = _agents.Where(a => a.Id != talker.Id)
            .OrderBy(_ => Guid.NewGuid()).First();

        var text = talker.CurrentAction switch
        {
            "eat_food"    => $"Really needed that. Hunger was getting bad.",
            "drink_water" => $"So thirsty. Drinking now.",
            "sleep"       => $"Exhausted. Going to rest for a bit.",
            "use_toilet"  => $"Had to go urgently.",
            "wander"      => $"Just wandering around. Mood's {talker.Drives.MoodLabel}.",
            "do_nothing"  => $"Not feeling great. Just sitting here.",
            _             => $"Doing {talker.CurrentAction}."
        };

        var msg = new ConversationMessage(now, talker.Id, listener.Id, text);
        _allConversations.Add(msg);
        talker.Messages.Add(msg);
        listener.Messages.Add(msg);

        // Cap conversation history
        if (_allConversations.Count > 500) _allConversations.RemoveAt(0);
    }
}
