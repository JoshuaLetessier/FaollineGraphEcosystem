# Changelog

All notable changes to **com.faolline.graphcore** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.7.0]

### Added
- **Guarded await — re-armable resume gate.** `BaseNodeData.ResumeConditions` (optional `List<BaseCondition>`,
  default empty): a parked await node now resumes on a matching `RaiseSignal` **only if all resume conditions
  pass** (AND; null entries skipped). A name match with a failing gate is **ignored and the node stays parked**
  (re-armable) — the actor can raise again once the context satisfies the gate. This expresses "press the button
  anytime, it only acts when the world is ready" *in the graph*, the re-arm semantics that gating an outgoing
  edge cannot give (the edge consumes the signal and gets stuck on a false gate). A direct host `Advance`/GoTo
  override is not gated.

### Notes
- Additive (MINOR); append-only. `AwaitSignalName`/`EntryConditions`/`RaiseSignal` and all other members
  unchanged; an await node with no resume conditions behaves byte-for-byte as before. From round-4 dogfooding
  (a consumer had to hand-wire `if (IsExitOpen) RaiseSignal("exit")` — now expressible as a resume gate).

## [0.6.0]

### Added
- **Timed waits**: `BaseNodeData.WaitDuration`, `BaseRunner.Tick(deltaSeconds)`,
  `RunnerState.WaitingForTime`, `OnWaitingForTime`. The host feeds elapsed time; a node holds until its
  duration elapses. Append-only.

## [0.4.0]

### Added
- **Signals** (host → runtime): `BaseNodeData.AwaitSignalName`, `BaseRunner.RaiseSignal`(+ scalar payload),
  `RunnerState.WaitingForSignal`, `OnWaitingForSignal`; a `BaseContext` signal channel
  (`RaiseSignal`/`OnSignal`/`OffSignal`/`TryGetLastSignal`, `SignalArgs`).
- **Collections**: named string-sets on `BaseContext`
  (`AddToCollection`/`RemoveFromCollection`/`CollectionContains`/`CollectionCount`/`GetCollection`/
  `ClearCollection`/`OnCollectionChanged`/`GetAllCollections`), deep-cloned by `DeepClone`. Append-only.

## [0.3.0]

### Added
- **Global + local execution contexts**: a sub-graph can ride the parent context with a fresh local overlay
  (`BeginLocalContext`/`EndLocalContext`; `SubGraphNodeData.OpensScope`). Local writes are discarded when the
  scope ends. Append-only on `BaseContext`/`BaseRunner`.

## [0.2.0]

### Added
- Collapsible **node groups** on the canvas (`GraphGroupData`, `BaseGroupView`).
- Reusable **GraphValidator** (Editor) + menu *Faolline ▸ Graph ▸ Validate Selected Graph*:
  flags missing/duplicate Start, invalid `EntryNodeId`, edges to/from missing nodes, isolated
  nodes, choices without options, and options with no outgoing edge.

### Fixed
- Node color is restored after a drag (UIElements timing); changing the color auto-enables the
  color override.

## [0.1.0]

### Added
- Data layer: `BaseGraph`, `BaseNodeData`, `BaseEdgeData`, built-in node types (Start/Statement/
  Choice/SubGraph/End), typed `ParameterData`, `BaseChoice`, `BaseCondition`, `BaseAction`.
- Execution runtime: headless `BaseRunner`, `BaseContext` blackboard, pluggable executors,
  sub-graph nesting, history, cycle detection.
- Editor: graph view, node/edge views, inspector, window; copy/paste with new GUIDs.
