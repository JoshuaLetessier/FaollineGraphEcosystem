# Phase 0 Research: Quest library (com.faolline.graphquest)

No `NEEDS CLARIFICATION` remained from the spec. This records the design decisions that shape Phase 1, grounded in
the existing ecosystem code that was read during planning.

## D1 — Delegate the prerequisite DAG to graphstandard's `ReactiveEvaluator`

- **Decision**: Represent a quest's objectives as nodes of a `QuestGraph : BaseGraph`, prerequisites as edges, and
  derive `Locked` / `Active` / `Completed` by running graphstandard's `ReactiveEvaluator` over it. `Available`
  maps to the quest `Active` state.
- **Rationale**: `ReactiveEvaluator` already does exactly the requested reactive derivation: it reads each edge
  `A→C` as "C requires A", tracks completion in a context string-set (the *completed-set*), derives state from
  topology + set, re-derives idempotently (`Reevaluate`), supports k-of-N gating, and raises
  `OnNodeAvailable`/`OnNodeCompleted`/`OnNodeLocked` (the last added for step-back re-locking). "Back" is a re-pass
  over a smaller set — exactly the spec's multidirectional model. The constitution (Principle V) forbids
  reimplementing what already exists.
- **Alternatives considered**: A bespoke quest evaluator (rejected — duplicates `ReactiveEvaluator`, diverges from
  the ecosystem's reactive progression, and re-solves cascading/re-lock that 026 already shipped).

## D2 — Completion is condition-driven; the condition records into the completed-set

- **Decision**: Each `ObjectiveNodeData` carries a graphcore `BaseCondition` *completion condition*. The
  `QuestEvaluator`, on each evaluation pass, checks every currently-`Available` objective's completion condition
  against the context; when it holds, the evaluator calls `ReactiveEvaluator.MarkCompleted(objectiveId)` (which
  records the id in the completed-set and cascades unlocks). Completion is therefore a *recorded fact* in the
  context, re-derived/restored through the same set the engine uses.
- **Rationale**: This bridges "completion is derived from the context" (the spec) with the engine's recorded
  completed-set. Reverting via history/save restore shrinks the set and re-locks downstream objectives (026's
  `OnNodeLocked`). Reuses graphcore `BaseCondition.Evaluate(BaseContext)` verbatim — no new condition language
  (FR-004).
- **Alternatives considered**: Pure per-pass re-derivation with no recorded set (rejected — `ReactiveEvaluator`
  needs the set to cascade; and a recorded set is what graphsave/history already persist).

## D3 — `Failed` is the graphquest overlay (the engine has three states)

- **Decision**: `ReactiveEvaluator` has `Locked`/`Available`/`Completed`. graphquest adds the fourth state
  `Failed`, tracked in a *failed-set* context collection. On each pass, before checking completion, the evaluator
  checks an objective's optional fail condition; if it holds, the id is recorded in the failed-set and the
  objective reports `Failed`. **Precedence: fail > complete** (if both hold on the same pass, the result is
  `Failed`). A quest is `Failed` when any *required* objective is `Failed`.
- **Rationale**: Keeps the engine untouched (Principle I/V) while satisfying the spec's four-state model. Recording
  fail in a collection makes it persist/restore symmetrically with completion.
- **Alternatives considered**: Pure-derived `Failed` (no set). Rejected for symmetry/persistence with completion;
  a recorded failed-set round-trips through graphsave like the completed-set, and keeps "already failed" stable.

## D4 — One-shot reward hooks guarded by a *rewarded-set*

- **Decision**: A quest/objective may carry a reward (`BaseAction`). When the evaluator observes an entity entering
  `Completed`, it checks a *rewarded-set* context collection; if the id is absent, it `Execute`s the reward against
  the context and records the id in the set. Subsequent passes (or a post-restore pass) see the id present and skip
  it — the reward fires **exactly once** (FR-008).
- **Rationale**: The rewarded-set lives in the context, so "already rewarded" persists through graphsave and
  survives restore (FR-012 / SC-004). The library owns only *when* the reward fires; the `BaseAction` content is
  consumer-supplied (FR-009).
- **Alternatives considered**: An in-memory "fired" flag (rejected — lost on restore, would re-grant rewards after
  load). Firing on `OnNodeCompleted` without a guard (rejected — `Start`/`Reevaluate` re-emit on restore).

## D5 — Persistence is automatic via context collections (no graphsave dependency)

- **Decision**: All quest state (completed-set, failed-set, rewarded-set) is held in graphcore context
  collections. graphsave's `GraphRunSnapshot` already serializes context collections, so capturing/restoring the
  context restores all quest progress with **no quest-specific snapshot fields and no hard graphsave dependency**.
  A test in the EditMode assembly references graphsave to *prove* the round-trip (FR-012), but the runtime package
  depends only on graphcore + graphstandard.
- **Rationale**: Smallest possible surface; avoids coupling the quest core to the optional save layer (the spec
  already framed persistence as "mostly piggybacks on the existing snapshot"). Confirmed against
  `GraphRunSnapshot.Capture`/`ApplyTo`, which copy `GetAllCollections()`.
- **Alternatives considered**: A `QuestProgressSnapshot` POCO + graphsave bridge (rejected — duplicates the context
  snapshot; only justified if quest state lived outside the context, which it does not).

## D6 — gameflow seam = "accept any `BaseContext`", no gameflow reference

- **Decision**: `QuestEvaluator` is constructed with a `BaseContext`. A gameflow host passes its live
  `GameFlowContext`; the quest core never references gameflow. (FR-013.)
- **Rationale**: `BaseContext` is the universal blackboard; the host already owns one. This is the same
  decoupling the rest of the ecosystem uses (no cross-domain references; Principle VII).
- **Alternatives considered**: A gameflow adapter package (deferred — not needed for v1; the plain constructor
  covers it).

## D7 — Code-first conditions/actions reuse graphstandard's standard SOs

- **Decision**: The fluent `QuestBuilder` accepts `BaseCondition`/`BaseAction` instances. Code-first authors create
  them via graphstandard's existing standard condition/action `ScriptableObject`s (`BoolCondition`,
  `IntCondition`, `CollectionContainsCondition`, `CollectionCountAtLeastCondition`, `SetBoolAction`,
  `AddToCollectionAction`, …) with `ScriptableObject.CreateInstance<T>()`, as graphstandard's own builders do.
- **Rationale**: Honors FR-004 (no new condition language) and Principle V (reuse). graphstandard already ships the
  needed primitives, so quest authoring needs no new conditions.
- **Alternatives considered**: A `Func<BaseContext,bool>`-backed condition (rejected for v1 — it would be a new
  condition vocabulary; if ergonomics demand it later it can be a graphstandard addition, not a graphquest one).

## D8 — Cycle rejection at build time

- **Decision**: `QuestBuilder` (or `QuestGraph` validation) rejects a prerequisite topology containing a cycle,
  with a `[GraphQuest]` diagnostic naming the cycle, before an evaluator is created (FR-006 / SC-007).
- **Rationale**: A cyclic prerequisite can never become `Available`; failing fast at authoring beats a silently
  stuck quest. graphcore already has cycle detection for sub-graphs (`CycleDetector`); a small DFS over the
  objective edges suffices here.
- **Alternatives considered**: Detecting at evaluation time (rejected — later, less clear, and the quest would just
  appear permanently `Locked`).

## Open items for `/speckit-tasks`

- Whether `QuestState` is one shared enum for quests and objectives (planned: yes, one enum) — confirmed in
  data-model.
- Exact evaluator API shape (single `Evaluate()` pass vs. auto-subscription to context changes): v1 ships an
  explicit `Evaluate()`/`Reevaluate()` plus `Action<...>` change events; auto-subscription to arbitrary context
  mutations is out of scope (the host calls `Evaluate` after it mutates the context, matching `ReactiveEvaluator`'s
  `MarkCompleted`/`Reevaluate` model).
