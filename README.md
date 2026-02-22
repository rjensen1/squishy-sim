# SquishySim

A biological drive simulation with a swappable LLM/rule-based decision layer. No game engine dependency — pure .NET 8.

## Architecture

```
SquishySim/
├── Body/
│   ├── BodyState.cs       # drives: hunger, thirst, fatigue, bladder, mood (floats 0.0–1.0)
│   └── DriveSystem.cs     # tick accumulator — drives increase over time
├── Actions/
│   ├── GameAction.cs      # action with delta effects on each drive
│   └── ActionCatalog.cs   # 6 actions: eat_food, drink_water, sleep, use_toilet, wander, do_nothing
├── Mind/
│   ├── IDecisionMaker.cs          # interface — swappable LLM vs rule engine
│   ├── PromptBuilder.cs           # serializes BodyState + ActionCatalog → Ollama prompt
│   ├── OllamaDecisionMaker.cs     # HTTP POST to Ollama, parses JSON response
│   └── RuleBasedDecisionMaker.cs  # deterministic urgency fallback (no Ollama needed)
└── Program.cs             # display loop + background sim tick
```

## Decision Interface

```csharp
public interface IDecisionMaker
{
    string DisplayName { get; }
    Task<(GameAction action, string reason)> ChooseAsync(BodyState state, IReadOnlyList<GameAction> actions);
}
```

Swap `OllamaDecisionMaker` ↔ `RuleBasedDecisionMaker` at startup. Future: add `ClaudeDecisionMaker`, `BehaviorTreeDecisionMaker`, etc.

## How to Run

```bash
# Rule-based (no Ollama needed):
dotnet run

# With Ollama (phi3 or any pulled model):
dotnet run -- --ollama --model phi3

# Options:
#   --model <name>   Ollama model name (default: phi3)
#   --url <url>      Ollama base URL (default: http://localhost:11434)
#   --tick <ms>      Milliseconds per tick (default: 2000)
```

## Design Notes

Drive rates are tuned so the creature hits "urgent" threshold after ~10-15 ticks of inaction. LLM prompt is minimal and model-agnostic. `OllamaDecisionMaker` extracts JSON from the LLM response defensively — handles markdown wrapping and extra text, falls back to the first action if parsing fails.

## Integration Points

- **SadConsole / roguelikes**: replace `Program.cs` display with your game's surface
- **Multiple creatures**: `List<(BodyState, IDecisionMaker)>` — the engine already supports it
- **Event-driven**: call `ChooseAsync` on specific game events rather than every tick
