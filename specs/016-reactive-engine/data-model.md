# Phase 1 — Data Model: P3 Reactive engine

New code lives in `com.faolline.graphstandard` (0.1.0). graphcore is unchanged.

## `ReactiveNodeState` (enum) — `Runtime/Reactive/ReactiveNodeState.cs`

```csharp
public enum ReactiveNodeState { Locked = 0, Available = 1, Completed = 2 }
```

- **Locked**: at least one prerequisite is not Completed.
- **Available**: no prerequisites, or all prerequisites Completed; and the node is not itself Completed.
- **Completed**: the node's id is in the completed-set collection.

## `ReactiveEvaluator` — `Runtime/Reactive/ReactiveEvaluator.cs`

Fields:
- `BaseGraph _graph`, `BaseContext _context`, `string _completedSetKey`.
- `Dictionary<string, List<string>> _prerequisites` — node id → its prerequisite node ids (sources of
  incoming edges), computed once at initialization from `_graph.Edges` (edge `From→To` ⇒ `From` is a
  prerequisite of `To`).
- `Dictionary<string, ReactiveNodeState> _states` — cached derived state per node, for transition detection.

Public members:
- `ReactiveEvaluator(BaseGraph graph, BaseContext context, string completedSetKey)` — stores refs, builds
  `_prerequisites`, and performs the initial evaluation (emitting events for initially non-Locked nodes).
- `event Action<string> OnNodeAvailable` — fired when a node enters Available (id passed).
- `event Action<string> OnNodeCompleted` — fired when a node enters Completed.
- `void MarkCompleted(string nodeId)` — if not already in the completed-set: `AddToCollection(_completedSetKey, nodeId)` then `Reevaluate()`; else no-op.
- `void Reevaluate()` — recompute all node states from the current completed-set; emit transition events.
- `ReactiveNodeState GetState(string nodeId)` — current derived state (Locked for an unknown id).
- `IReadOnlyCollection<string> AvailableNodeIds` — ids currently Available.
- `IReadOnlyCollection<string> CompletedNodeIds` — ids currently Completed.

### Derivation rule (per node `n`)

```
if _context.CollectionContains(_completedSetKey, n.Id)         -> Completed
else if prerequisites(n) is empty
     OR every prereq p is in the completed-set                 -> Available
else                                                            -> Locked
```

### Initialization & Reevaluate

- `Initialize` (in ctor): compute state for every node; for each node whose state is Available emit
  `OnNodeAvailable`, whose state is Completed emit `OnNodeCompleted`; seed `_states`.
- `Reevaluate`: recompute every node's state into a temp map; for each node whose new state differs from
  `_states`, emit the entry event for Available/Completed (Locked transitions emit nothing); then replace
  `_states` with the new map.

**Invariants**
- E1 — **Set-derived state**: a node's state is a pure function of (topology, completed-set). No hidden
  per-node persisted state; the cache is only for transition detection (FR-002, FR-008).
- E2 — **AND prerequisites**: Available requires ALL prerequisites Completed; empty prerequisites ⇒
  Available (FR-002, SC-001).
- E3 — **Idempotent completion**: `MarkCompleted` on an id already in the set is a no-op — no duplicate,
  no `Reevaluate`, no events (FR-003, SC-003).
- E4 — **Transition-only events**: an event fires only when a node's state actually changes; re-evaluation
  with no net change emits nothing (FR-004).
- E5 — **Init emission**: initialization emits Available for initially-available nodes and Completed for
  initially-completed nodes (FR-005).
- E6 — **Reversible / re-pass**: after the completed-set shrinks (e.g. the host restores the context via
  graphcore history then calls `Reevaluate`), states recompute to the smaller satisfied set; no node undo
  side-effects run (FR-008, SC-004).
- E7 — **Durable**: completion lives only in the P2 collection, so it persists and history-restores via
  graphcore; the evaluator re-derives from the restored context (FR-009).
- E8 — **No traversal**: no current node, no Proceed/Choose; many nodes can be Available at once (FR-007).
- E9 — **Robust to bad ids / cycles**: an id in the set but absent from the graph is ignored; mutually
  dependent nodes simply never become Available (no traversal ⇒ no infinite loop) (edge cases).

## Package / assemblies (created by this feature)

- `com.faolline.graphstandard/package.json` — name `com.faolline.graphstandard`, version `0.1.0`,
  `dependencies: { "com.faolline.graphcore": "0.0.0" }` (project-local resolution, matching the dialogue lib).
- `Runtime/com.faolline.graphstandard.Runtime.asmdef` — rootNamespace `Faolline.GraphStandard`,
  references `["com.faolline.graphcore.Runtime"]`.
- `Tests/EditMode/com.faolline.graphstandard.Tests.EditMode.asmdef` — references the Runtime + graphcore +
  TestRunner assemblies; `includePlatforms:["Editor"]`, `testPlatforms:["EditMode"]`, `autoReferenced:false`,
  `overrideReferences:true`, `precompiledReferences:["nunit.framework.dll"]`.

graphcore: **no changes**.
