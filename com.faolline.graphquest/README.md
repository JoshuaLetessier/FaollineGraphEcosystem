# Faolline GraphQuest

**Version**: 0.9.2 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore`, `com.faolline.graphstandard`, `com.faolline.graphlocalization`

`com.faolline.graphquest` — a **quest & objective** domain library above
[`com.faolline.graphcore`](../com.faolline.graphcore) and [`com.faolline.graphstandard`](../com.faolline.graphstandard).

Quests and objectives whose states — **`Locked` / `Active` / `Completed` / `Failed`** — are **derived from the
shared `BaseContext`**, not stored by hand. One model covers two quest shapes:

- **Reactive objective DAGs** — objectives gate each other via prerequisites; state is recomputed from the context.
- **Staged / sequential quests** — a linear prerequisite chain in the same model.

Both run on graphstandard's `ReactiveEvaluator` (edges = prerequisites). "Back" is a re-pass (re-derive from a
smaller completed-set), never an undo.

## What it adds over the engine

- **Completion / fail conditions** per objective — graphcore `BaseCondition`s. When completion holds, the objective
  is recorded done (cascading unlocks); a fail condition gives the fourth `Failed` state (fail precedes complete).
- **One-shot reward hooks** — graphcore `BaseAction`s fired **exactly once** on completion (guarded by a context
  "rewarded" set, so they never re-fire on re-evaluation or after a load).
- **Quest aggregation** — a quest completes when all its *required* objectives complete; an unlock condition gates
  the whole quest; optional objectives track state + rewards without blocking completion.
- **Code-first fluent builder** — `QuestBuilder`; cyclic prerequisites are rejected at `Build()`. Prerequisites are
  all-of-N (`Requires("a", "b")`) or **k-of-N** (`RequiresAtLeast(2, "a", "b", "c")` — unlocks at any two).
- **Replay** — `QuestEvaluator.Reset()` clears a quest's scoped progress (other quests on the same context are
  untouched), so it can be played again and one-shot rewards fire anew.
- **Journal data layer** — name/describe objectives (`.Named(...)` / `.Describe(...)`) and the quest; read
  `QuestEvaluator.GetObjectives()` (`ObjectiveView`: id + label + description + required + state) and
  `RequiredCompleted`/`RequiredTotal` to drive a quest-log UI without keeping your own id→label table.
- **Cross-quest chaining** — `QuestBuilder.Create("B").UnlockAfter("A")` keeps quest B Locked until quest A
  completes (synced through a shared context set, so it persists and reverts like everything else).
- **Time-limited objectives** — `.WithTimeLimit(seconds)`; the host ticks `Evaluate(now)` with a game clock and an
  objective Fails if it isn't completed in time. `GetRemainingSeconds(id)` drives a countdown.
- **Localized text (optional)** — `evaluator.UseLocalization(provider)` resolves objective/quest names &
  descriptions as keys via `com.faolline.graphlocalization` (CSV or Unity Localization); without a provider the
  text stays literal.
- **Quantified objectives** — `.ProgressCollectionKey("items").ProgressTarget(10)` tracks a collection count
  against a target; `ObjectiveView` exposes `Progress` / `ProgressTarget` (e.g. "3/10" in a journal UI).
- **Abandon** — `QuestEvaluator.Abandon()` drops the quest to `QuestState.Abandoned`, for player-initiated
  quest abandonment. Re-activatable via `Reset()` if the design allows replaying.

## Quick example

```csharp
using Faolline.GraphCore;
using Faolline.GraphStandard;   // standard BaseConditions/BaseActions
using Faolline.GraphQuest;

QuestGraph rescue = QuestBuilder.Create("rescue")
    .AddObjective("find_clue").CompleteWhen(/* BaseCondition */)
    .AddObjective("pick_lock").Requires("find_clue").CompleteWhen(/* ... */)
    .AddObjective("escape").Requires("pick_lock").CompleteWhen(/* ... */)
    .RewardQuestWith(/* BaseAction */)
    .Build();

var ctx = new QuestContext();              // or a host's BaseContext / GameFlowContext
var quest = new QuestEvaluator(rescue, ctx);

quest.OnObjectiveStateChanged += (id, s) => Debug.Log($"[{id}] {s}");
ctx.Set<bool>("found_clue", true);
quest.Evaluate();   // find_clue -> Completed, pick_lock -> Active, ...
```

See [the spec quickstart](../specs/029-graph-quest/quickstart.md) for the full walkthrough (gating, rewards-once,
save/restore, host context).

## The observe / derive contract (don't double-write "completed")

`QuestEvaluator` **observes** your game state and **derives** quest state from it — it never writes back into
your game's data. Your game owns the world (e.g. a `solvedPuzzles` collection, a `boss_defeated` flag); each
objective's `CompletionCondition` reads that world, and the evaluator records the *derived* result into its own
scoped bookkeeping (`quest_completed:<questId>`, etc.). So it is normal and correct to see **two** "done"
signals — your game's own (`solvedPuzzles`) and the quest's derived set — and you should **not** try to keep
them in sync by hand:

```csharp
// Game code writes ONLY its own world state:
context.AddToCollection("solvedPuzzles", "altar");

// The objective observes it — you never write quest_completed yourself:
QuestBuilder.Create("temple")
    .AddObjective("altar").CompleteWhen(new CollectionContainsCondition { /* solvedPuzzles ∋ altar */ })
    .Build();

eval.Evaluate();  // derives quest_completed:temple ∋ altar from the world
```

**Why the separation is deliberate — don't "fix" it with a `MarkCompleted`:**

- **Idempotence & replay.** Because completion is *re-derived* every `Evaluate()`, a pass over unchanged state
  produces no duplicate transitions, and "back = re-pass" (rewinding the world re-derives the quest). A
  direct `MarkCompleted(objectiveId)` write would make quest state mutable out-of-band and break both.
- **One source of truth.** The world is authoritative; the quest is a projection. Two writers for the same
  fact is exactly the bug you'd be inviting.
- **Retry is still granular.** To replay one objective, rewind its *world inputs* and call
  `QuestEvaluator.ResetObjective(id)` (clears the derived bookkeeping + re-arms its timer) — you still never
  write the completed-set directly.

If you genuinely need an imperative "mark this done" (no observable world fact backs it), model that fact
explicitly — e.g. set a `bool` the objective's `CompletionCondition` reads — rather than writing the quest's
derived set.

## Persistence & host

All quest state lives in `BaseContext` collections, so a `com.faolline.graphsave` context snapshot restores quest
progress with **no quest-specific snapshot type and no runtime graphsave dependency**. `QuestEvaluator` takes any
`BaseContext`, so a gameflow host can drive quests on its own blackboard — **graphquest references neither graphsave
nor gameflow at runtime**.

## Visual editor

Besides the code-first builder, the package ships a `QuestGraphEditorWindow` (open via
`Faolline ▸ Open Quest Graph Editor`, or double-click a `QuestGraph` asset): objective nodes, drawable prerequisite
edges (From→To = "To requires From"), and an inspector for the objective + quest-level fields.

**No Start/End node — by design.** A quest is a *reactive objective DAG*, not a runner-walked graph: there is no
traversal cursor, so there is no entry (Start) or exit (End) node. The "begin" is the quest's `UnlockCondition`
plus the objectives that have **no prerequisite**; the "end" is the **completion aggregate** (all required
objectives done). To keep that readable, the editor marks each objective that is an **entry** (no prerequisite) or
**terminal** (nothing depends on it) right on the node — these cues refresh on Save / ↻ Refresh after you draw or
remove a prerequisite edge.

## Boundaries

- The library ships **data + change events + reward seams only** — the in-game quest journal / tracker UI is yours.

## Dependencies

- `com.faolline.graphcore` 0.14.0
- `com.faolline.graphstandard` 0.10.1
- `com.faolline.graphlocalization` 0.4.0 (the neutral text-resolution abstraction; localization is opt-in at
  runtime via `UseLocalization`, the CSV provider needs no Unity Localization)
