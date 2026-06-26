# TODO / status — `talk` verb + LLM brains

Working branch: **`feature/dana-verb-menu`** (off `main`). Last session: 2026-06-26.

## Done & verified

- **Parameterized `talk` verb** — agent says a specific line to a specific agent.
  - `GameAction.RequiresTarget` / `RequiresContent` flags; `talk` in `ActionCatalog`.
  - `DecisionResult` record (`Action, Reason, Target?, Content?`) with a back-compat
    2-arg `Deconstruct` so old `(action, reason)` call sites compile untouched.
  - `IDecisionMaker.ChooseAsync` gains optional `targets` roster param.
  - `PromptBuilder` lists addressable agents + teaches the `{action,target,content,reason}` JSON.
  - `OllamaDecisionMaker.TryParseDecision` (public/static) — validates target+content,
    one pointed-correction retry, then deterministic urgency fallback.
  - `SimulationService.ResolveTalk` — routes `talk` to a `ConversationMessage` carrying the
    LLM's actual line, social credit to both parties.
- **LM Studio brain** — `LmStudioDecisionMaker` (OpenAI `/v1/chat/completions`), reuses the
  shared `PromptBuilder` + `TryParseDecision`. `SetLlmConfig` routes a `/v1` (or `:1234`)
  base URL → LM Studio, else Ollama.
- **Prompt reframe** — intro now "you ARE a person, not just a body"; social need is a
  first-class reason to act. (Was: "choose the best action given its biological state",
  which buried the social verbs.)
- **LLM menu** — `socialize` removed from the menu LLM agents see; for a model, `talk`
  supersedes it (socialize is the rule-based auto-seek primitive). Rule-based agents
  ignore the passed menu, so they still socialize as before.
- **Tests: 120/120 green** (9 parser + 3 end-to-end talk tests added; 2 FakeDecisionMakers migrated).
- **Live end-to-end confirmed**: Alice on **Qwen3-14B** chose `talk` and authored her own
  lines to Bob/Charlie. The full pipeline works against a real model.

## Key finding: model size is the gate

- **phi-4-mini-instruct (3.8B) is too weak** for this. With *all* drives pinned to 0 it
  still pattern-defaults to `eat_food` and invents "satiate hunger" — it doesn't actually
  read the state. Never chose `talk`.
- **Qwen3-14B chose `talk` within a few steps.** Recommend a **>=14B** instruction-follower
  as the floor for LLM agents (Qwen3-14B fits the 5080; Qwen3-30B-A3B is the smart/fast MoE option).
- The social-isolation-coherence degradation (high social >0.65 distorts the perceived
  snapshot) is real but was NOT the main blocker — the live numbers showed phi-4 ignoring
  even true-zero drives. Model weakness dominated.

## Open TODOs

1. **Proximity-gate `talk`.** Right now it "shouts across the map" — lands immediately on the
   named agent regardless of distance (see comment in `ResolveTalk`). Make it walk to the
   target first, then speak, reusing the existing `Seeking` -> `SocialRange` machinery.
2. **Speech/thought bubbles in the UI.** Render `content` as a speech bubble and `reason` as
   a thought bubble. (`wwwroot` is gitignored / not in the repo — the live UI lives locally.)
3. **Two-way conversation.** `talk` is one-directional; the target doesn't reply. Consider a
   reply turn so exchanges thread.
4. **Document the `socialize` vs `talk` split** (rule-based gets socialize, LLM gets talk) or
   unify them. Right now it's a deliberate split, easy to trip over.
5. **Model floor / default.** Default LLM agents to a >=14B model, or surface a warning when a
   sub-~7B model is selected. phi-4-mini gives a bad first impression.
6. **`LmStudioDecisionMaker` has no direct unit test** — the shared `TryParseDecision` is
   covered, but the chat-completions transport isn't. Consider a smoke test guarded on server
   availability.
7. **Open the PR** for `feature/dana-verb-menu` once the above are triaged.

## Env note

- This box (home) now has the **.NET 8 SDK (8.0.422)** installed — previously only the runtime
  + SDK 6.0, which couldn't build net8.0. `dotnet` is at `C:\Program Files\dotnet`.
- Run locally: `dotnet run --project SquishySim.Api.csproj` -> http://localhost:5300 (no `wwwroot`
  in repo, so root 404s; hit `/agents`, `/conversations`, `/sim/status`).
- Switch an agent to an LLM:
  `PUT /agents/{id}/llm  {"model":"qwen/qwen3-14b","baseUrl":"http://localhost:1234/v1"}`
  (LM Studio: Developer tab -> load model -> Start Server, port 1234).
