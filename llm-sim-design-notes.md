# Design notes — LLM-driven life sim

Research dump from a 2026-06-26 session with Claude. The long-term idea: a
SimCity/Sims-style world where, **when a sim hits a "life event," its stats
(skills, happiness, money, etc.) get serialized and handed to a small local
LLM, which picks from a set of discrete options.** Two existing repos each hold
half of this — `EconSim` (citizens, firms, houses, jobs, birth/immigration/
lifecycle events) and `squishy-sim` (`BodyState` drives → `IDecisionMaker` →
`ActionCatalog` of discrete actions → Ollama). The full idea is those two
welded together.

## The thing already exists as research: Smallville

Stanford's **Generative Agents** (a.k.a. *Smallville*, Park et al. 2023): 25
agents walk a 2D town, greet each other, form relationships, plan their days,
self-reflect — all driven by an LLM reading each agent's state + memory. That's
this project, more or less, already prototyped academically. Worth reading for
the **memory → reflection → planning loop**, even if none of the code gets used.

## Why our architecture is the *better* version

Smallville feeds *everything* to a big cloud LLM every tick:
- slow
- expensive
- hallucinates freely, because there's no menu — it narrates whole lives

Our design is the production-grade inversion of that:

| Smallville | This project |
|---|---|
| Every-tick LLM call | **Life-event–triggered** only |
| Free-form narration out | **Discrete options** out (pick 1 of N) |
| Big cloud model | **Small local model** (Ollama / LM Studio) |
| Implicit state in prose | **Explicit stats** serialized in |

**Discrete options is the key move.** It turns the LLM from "narrate a believable
life" (where small models faceplant) into "given *who this sim is*, pick one of
these four" — where even a 2–4B model shines. `squishy-sim`'s `ActionCatalog` +
`IDecisionMaker` is already exactly this shape; the job is to scale it from one
body's drives to a citizen's life events.

### Prompt craft gotcha (small models)
Don't tell a small instruct model to **"pretend you are X."** It's RLHF'd to be a
helpful assistant and "pretend" is the verb it's trained hardest to refuse — it
bounces the role back onto the user. Instead: give it an **identity, not an
instruction** ("You ARE Dana, an 18-year-old…"), respond in first person, and
leave no floating "you" for it to reattach to the user. If it drifts back to
assistant-mode after a few turns (tell: it starts summarizing or listing), add
"stay in the moment; react to what just happened, don't summarize."

## Open-source Sims-like games to borrow from (not adopt wholesale)

- **FreeSO** — full open-source reimplementation of *The Sims Online* (C#/Monogame).
  Official servers shut down Dec 2024; now a preservation/tech base. Read its
  needs/motives + room/object interaction system.
- **OpenTS2** — open-source reimplementation of *The Sims 2* in Unity. Earlier stage.
- **Micropolis** — the *actual* SimCity engine, GPL'd by EA/Maxis. The city layer
  under the sims, if it ever wants one.

## The steer

Don't adopt a whole engine. The agents-walking-and-greeting part already exists.
Keep the home-grown sim, read **Smallville for the decision-loop wiring**, and
bolt on **local-LLM-with-discrete-options** as the brain. The hard part isn't the
game — it's the prompt that turns
`{happiness: 0.3, skills: [...], event: "got fired"}` into a clean menu pick.

## Sources

- [Stanford Generative Agents (Smallville) repo](https://github.com/joonspk-research/generative_agents)
- [claudeville — Smallville ported to Claude](https://github.com/AlexHarn/claudeville)
- [FreeSO — The Sims Online reimplementation](https://github.com/riperiperi/FreeSO)
- [OpenTS2 — The Sims 2 in Unity](https://github.com/LazyDuchess/OpenTS2)
- [Micropolis — open-source SimCity](https://github.com/SimHacker/micropolis)
