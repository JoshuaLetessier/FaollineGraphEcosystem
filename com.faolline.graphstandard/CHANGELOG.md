# Changelog

All notable changes to **com.faolline.graphstandard** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.7.0]

### Added
- **`GraphNodeBuilder.ResumeWhen(params BaseCondition[])`** — fluent authoring of an await node's resume gate
  (graphcore 0.7.0): `AddStatement(...).Await("exit").ResumeWhen(cond)`. Appends to the node's
  `ResumeConditions` (all must pass for a matching signal to resume; a failing gate keeps the node parked,
  re-armable). Mirrors `When` (entry conditions).

### Changed
- Dependency: `com.faolline.graphcore` `0.6.0 → 0.7.0` (the guarded-await capability the sugar targets).

### Notes
- Additive (MINOR); append-only. From round-4 dogfooding (option ii — gate a Linear signal on context state,
  in-graph).

## [0.6.0]

### Added
- **`ReactiveEvaluator.OnNodeLocked`** — a symmetric re-lock event (`Action<string>`), the Locked-state
  counterpart of `OnNodeAvailable`/`OnNodeCompleted`. Fires on a backward transition during `Reevaluate`
  (step-back / replay drops the completed-set below a node's threshold) and once per initially-Locked node
  during `Start()`, so a host can react to re-locking without inferring it. Routed through the existing
  emission choke point — identical firing semantics to the other two events; derivation unchanged.

### Changed
- **README** — the reactive-hosting section now leads with `MarkCompleted` (own the evaluator) and states
  explicitly that `MarkCompleted` and the `OnCollectionChanged → Reevaluate` bridge are **alternatives**, not
  to be combined (the bridge is only for the *action-writes-the-set* path; combining double-evaluates). From
  round-4 dogfooding (a consumer needed a re-read to see the two paths were exclusive).

### Notes
- Additive (MINOR); graphcore + gameflow untouched; existing public API append-only.

## [0.5.0]

### Added
- **Universal collection primitives** (Runtime) — authorable standard nodes/edges for graphcore's string-set
  collections, promoting what previously lived only in the `graphTest` fixtures into the real lib:
  - **`AddToCollectionAction`** (`GraphStandard/Actions/Add To Collection`): a node enter/exit action that
    records a configured value into a configured collection key. Idempotent; a graceful no-op on empty
    key/value.
  - **`CollectionContainsCondition`** (`GraphStandard/Conditions/Collection Contains`): satisfied when the
    collection contains a configured value (absent key ⇒ false).
  - **`CollectionCountAtLeastCondition`** (`GraphStandard/Conditions/Collection Count At Least`): satisfied
    when the collection's count reaches a threshold — how a "k-of-N done unlocks this" gate is expressed on a
    Linear edge (threshold 0 ⇒ always true; absent key ⇒ count 0).

### Notes
- Additive (MINOR); graphcore + gameflow + graphTest untouched (the graphTest collection fixtures remain as the
  reactive-engine test reference). From round-3 dogfooding (the slice-5 boot-seam growth path made usable).
- **Reactive-hosting pattern** (README): a Linear flow records completion through `AddToCollectionAction` into a
  shared *completed-set*; a `ReactiveEvaluator` over the **same** context (k-of-N via `requiredCounts`) derives
  the unlocks; the consumer bridges with a two-line `BaseContext.OnCollectionChanged(key, _ => evaluator.Reevaluate())`,
  and shares the context with the driver via the gameflow `Boot(context, registry)` seam. No bespoke action,
  condition, or engine needed. A turnkey wrapper that owns the evaluator is deferred until dogfooding justifies it.

## [0.4.0]

### Added
- **`GraphBuilder<TGraph>`** (Runtime): a public fluent, code-first builder for any `BaseGraph` subclass over
  graphcore's universal types — `AddStart/AddStatement/AddChoice/AddSubGraph/AddEnd` return a node handle
  (`Title/At/OnEnter/OnExit/When/Await/Wait/Checkpoint/Choice/AsEntry/To`); `Edge`/`Build`. Auto-GUID ids,
  auto-column positions; choice edges route by choice title. Adds no runtime behavior. Replaces the
  GUID/`AddNode`/`AddEdge` boilerplate consumers previously copied from an internal sample.
- **`GraphAssetBuilder.Save(graph, path)`** (new Editor assembly `com.faolline.graphstandard.Editor`):
  persists a graph as an asset with its attached actions/conditions as sub-assets (only objects not already
  persisted), self-contained and reloadable.

### Notes
- Additive (MINOR); graphcore untouched. From round-2 dogfooding — code-first graph construction was the
  remaining ergonomic gap (hit in both rounds).

## [0.3.0]

### Added
- **Flow engine** (`FlowRunner`) — the third execution engine: cursor-less and multi-active. Firing a node
  runs its `OnEnterActions`, emits `OnNodeFired`, then **forks** to every condition-passing outgoing edge; a
  node with multiple incoming edges **joins** on a k-of-N rendezvous (default = all incoming = AND).
  Re-pass is intentional (cycles bounded by a fire-count safety cap that warns); per-node **one-shot** marks
  fire at most once until `Reset`. Join thresholds and one-shot are constructor config — graphcore untouched.

### Fixed
- `FlowRunner` join bookkeeping now uses a stable per-edge token assigned at construction instead of
  `BaseEdgeData.Id`. A graph built in code with empty edge ids previously collapsed distinct incoming edges
  into one bucket, deadlocking an AND-join (or firing an OR-join too eagerly).
- `FlowRunner` cascade is now driven by an explicit work queue instead of recursion, so a deep or wide flow
  cannot overflow the call stack before reaching the safety cap.

## [0.2.0]

### Added
- **Generic threshold join** for the Reactive engine: `ReactiveEvaluator` takes an optional
  `requiredCounts` map (node id → k). A node becomes Available when at least *k* of its *N* prerequisites are
  Completed. `k = N` is AND (the default for any unlisted node), `k = 1` is OR, `1 < k < N` is N-of-M,
  `k ≤ 0` is ungated, `k > N` never auto-available. Additive and source-compatible — all 0.1.0 callers keep
  the default AND behavior.

## [0.1.0]

### Added
- Initial release of the buffer library above `com.faolline.graphcore`.
- **Reactive engine** (`ReactiveEvaluator`, `ReactiveNodeState`): cursor-less prerequisite/progression DAG.
  Reads each edge as a prerequisite and derives `Locked | Available | Completed` from graph topology plus a
  completed-set collection on the shared `BaseContext`. `MarkCompleted` cascades unlocks and raises
  `OnNodeAvailable` / `OnNodeCompleted`; `Start` emits the initial state; `Reevaluate` re-derives idempotently
  after a host step-back.
