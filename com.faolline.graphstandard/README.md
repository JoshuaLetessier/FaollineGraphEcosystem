# com.faolline.graphstandard

**Version**: 0.15.1 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.27.0

Buffer library **above** `com.faolline.graphcore`. graphcore is the universal **data substrate** (graph,
nodes, edges, conditions, actions, context) plus the **Linear** reference runner (`BaseRunner`, single
cursor, select-one). graphstandard hosts the **non-linear execution engines** that read the *same* graph
data with different semantics — and, in the future, domain-neutral standard nodes built on top of them.

graphcore is never modified by anything here: every engine is pure C# over graphcore's public API, and all
per-engine configuration (prerequisite thresholds, join thresholds, one-shot marks) is supplied to the
engine constructor rather than stored on graphcore node fields.

---

## Which engine when?

The same `BaseGraph` can be driven by three different engines. Pick by how progression actually behaves:

| If progression is… | Use | Engine | An edge means | Typical use |
|--------------------|-----|--------|---------------|-------------|
| **one place at a time**, you pick the next step | `BaseRunner` *(graphcore)* | **Linear** | the next step (select one) | narrative, cinematics, scene-flow, dialogue |
| **a dependency web**, many things open at once as you complete others | `ReactiveEvaluator` | **Reactive** | a *prerequisite* (`A→C` = "C requires A") | quests, skill-trees, region/area unlock, achievements |
| **many things firing together** from one trigger, branches that re-converge | `FlowRunner` | **Flow** | a *flow connection* (fire the source ⇒ propagate to the target) | ability/spell execution, simultaneous effects, multidirectional sequences |

Rules of thumb:

- **One cursor, you choose → Linear.** If "where am I" is a single node, it's Linear.
- **No cursor, state is derived from what's done → Reactive.** If "what's open" is a *set* that grows as you
  complete prerequisites, and "back" means re-deriving from a smaller completed-set (a re-pass, not an undo),
  it's Reactive.
- **No cursor, many active at once from one fire → Flow.** If firing one node should light up *all* valid
  successors and reconverge at a join (cast → damage + debuff + vfx → cooldown), it's Flow.

The three are composable: a Linear scene-flow can host a Reactive progression for its objectives and fire a
Flow ability — all sharing one `BaseContext`. Cross-library nesting still goes through graphcore's SubGraph.

> **Live run-cursor (0.9.0)**: while playing, `ReactiveEvaluator` and `FlowRunner` self-register a graphcore
> editor probe, so the graph editor window paints their live state on the open graph — reactive nodes as
> Locked/Available/Completed, flow nodes as the fired set. Editor-only, compiled out of player builds.

---

## Building graphs in code (`GraphBuilder`)

Construct any `BaseGraph` subclass fluently — no GUID ids, no `AddNode`/`AddEdge` boilerplate:

```csharp
using Faolline.GraphStandard;

var b = new GraphBuilder<GameFlowGraph>();
var start = b.AddStart().AsEntry();
var room  = b.AddStatement("Load Room").OnEnter(loadRoom).Wait(2f);   // a timed intro
var lever = b.AddStatement("Await lever").Await("lever").ResumeWhen(hasKey);   // gated, re-armable
var end   = b.AddEnd("Done");
start.To(room); room.To(lever); lever.To(end);
GameFlowGraph graph = b.Build();
```

`AddStart/AddStatement/AddChoice/AddSubGraph/AddEnd` return a node handle; `Title/At/OnEnter/OnExit/When/
Await/ResumeWhen/Wait/Checkpoint/Choice/AsEntry/To` configure it (each returns the handle); `Edge`/`To` wire nodes;
`Build()` returns the typed graph. Ids are auto-GUID, positions auto-column. Choice edges route by the choice
**title**: `c.Choice("Win", cond); b.Edge(c, winNode, "Win");`. The builder adds **no** runtime behavior — a
built graph runs exactly like a hand-assembled one. Wiring a node from another builder throws.

**Persist it as an asset** (editor) with its actions/conditions stored as sub-assets, self-contained:

```csharp
using Faolline.GraphStandard.Editor;
GraphAssetBuilder.Save(graph, "Assets/MyGame/Flow.asset");
```

### A looping game-shell is a supported pattern

A game shell (menu → play → win → back to menu → replay) never "ends" — model it as a **cyclic Linear graph
with no End node**: the runner follows the single outgoing edge on each advance and loops forever. The flow
stays running (no `OnEnded`); history is bounded by `BaseGraph.HistoryDepth`, so set a small depth for a
forever-looping shell (`GoBack` across the loop isn't meaningful).

---

## Architecture

```
com.faolline.graphstandard
│
├── Runtime/
│   ├── Reactive/
│   │   ├── ReactiveNodeState     Locked | Available | Completed
│   │   └── ReactiveEvaluator     Cursor-less prerequisite/progression DAG (k-of-N threshold join)
│   └── Flow/
│       └── FlowRunner            Multi-active token-propagation engine (fork / join / re-pass / one-shot)
│
└── Tests/EditMode/
    ├── Reactive/                 Threshold join, cascade, events, Start/Reevaluate
    └── Flow/                     Fork, join, re-pass, one-shot, cycle cap, ability scenario
```

---

## Reactive engine — `ReactiveEvaluator`

Cursor-less. Reads each edge as a **prerequisite** and derives every node's `ReactiveNodeState` from graph
topology plus a **completed-set** — a graphcore string-set collection on the shared `BaseContext` (so
completion persists and history-restores via graphcore). Many nodes may be `Available` at once.

```csharp
// "puzzle1" and "puzzle2" must be done before "region2" opens.
graph.AddEdge(new BaseEdgeData { FromNodeId = "puzzle1", ToNodeId = "region2" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "puzzle2", ToNodeId = "region2" });

var ctx  = new BaseContext();
var eval = new ReactiveEvaluator(graph, ctx, completedSetKey: "completed");

eval.OnNodeAvailable += id => Debug.Log($"unlocked: {id}");
eval.OnNodeCompleted += id => Debug.Log($"done: {id}");
eval.OnNodeLocked    += id => Debug.Log($"locked: {id}");   // symmetric re-lock (re-pass / step-back)
eval.Start();                       // initial emission, after subscribing

eval.MarkCompleted("puzzle1");      // region2 still Locked (needs both — AND default)
eval.MarkCompleted("puzzle2");      // region2 → Available, OnNodeAvailable("region2")

eval.GetState("region2");           // ReactiveNodeState.Available
eval.AvailableNodeIds;              // ids currently Available
```

### k-of-N threshold join

By default a node needs **all** its prerequisites (AND). Pass a per-node required count `k` to generalize:

```csharp
// "region" opens when ANY 2 of its 3 member puzzles are done (N-of-M).
var eval = new ReactiveEvaluator(graph, ctx, "completed",
    requiredCounts: new Dictionary<string, int> { ["region"] = 2 });
```

| `k` | Meaning |
|-----|---------|
| `k = N` *(default)* | AND — all prerequisites |
| `k = 1` | OR — any one prerequisite |
| `1 < k < N` | N-of-M |
| `k ≤ 0` | ungated (Available unless Completed) |
| `k > N` | never auto-available (host-completed only) |

`MarkCompleted` records completion and cascades unlocks. After the host restores a *different* completed-set
(a step-back / un-complete), call `Reevaluate()` to re-derive — derivation is idempotent and reversible:
state depends only on the current set, never on history. A node dropping back to `Locked` on a re-pass raises
`OnNodeLocked`, symmetric with `OnNodeAvailable`, so UI can react to a reset without a manual repaint.

> **Gating a Linear flow on progression?** Gate on the **completed-set** directly — a
> `CollectionCountAtLeastCondition { completed, k }` on an await's `ResumeConditions` or a choice branch is the
> "k-of-N done unlocks this" gate, and it needs no evaluator. You only need the evaluator's *derived* `Available`
> state (cascades, per-node `requiredCounts`) for UI/eventing. If a future case ever needs to gate a Linear
> await/edge on that derived availability, mirror `AvailableNodeIds` into a context collection (e.g. on
> `OnNodeAvailable`/`OnNodeLocked`) so a `CollectionContainsCondition` can read it — not built in, by design
> (no real case yet).

---

## Standard collection primitives

Authorable standard nodes/edges over graphcore's string-set collections — attach them as assets, no scripting:

| Primitive | Kind | Effect |
|-----------|------|--------|
| `AddToCollectionAction` | action (on-enter/on-exit) | adds a configured value to a configured collection key (idempotent; no-op on empty key/value) |
| `RemoveFromCollectionAction` | action (on-enter/on-exit) | removes a configured value from a configured collection key (no-op if absent) |
| `ClearCollectionAction` | action (on-enter/on-exit) | removes all values from a configured collection key |
| `CollectionContainsCondition` | condition | satisfied when the collection contains a configured value (absent key ⇒ false) |
| `CollectionCountAtLeastCondition` | condition | satisfied when the collection's count ≥ a threshold (threshold 0 ⇒ always true; absent key ⇒ 0) |

These are the universal write/read half of progression: a node *records* into a collection; an edge *gates* on it.

### Hosting a reactive progression on a shared context

Compose the **Linear driver** (gameflow), the **`ReactiveEvaluator`**, and (optionally) these **collection
primitives** into a live progression on one shared blackboard — no bespoke action, condition, or engine:

```csharp
// progressionGraph: edges encode prerequisites; requiredCounts gives k-of-N (2-of-3 unlocks "exit").
var ctx  = new GameFlowContext();
var eval = new ReactiveEvaluator(progressionGraph, ctx, "completed",
    requiredCounts: new Dictionary<string, int> { ["exit"] = 2 });

eval.OnNodeAvailable += id => Debug.Log($"available: {id}");

driver.BootOnStart = false;
driver.Boot(ctx);            // gameflow Boot(context, registry) seam — the flow runs on the SAME ctx
eval.Start();

eval.MarkCompleted("roomA"); // records into the completed-set AND re-derives — "exit" opens on the 2nd
```

#### Recording completion — pick one path, not both

- **Own the evaluator (simplest, recommended):** call `eval.MarkCompleted(id)`. It writes the completed-set
  **and** re-derives for you — this is all you need when your own code drives completion.
- **A Linear flow writes the set:** put an `AddToCollectionAction { completed, id }` on a node and bridge the
  write to re-derivation:

  ```csharp
  ctx.OnCollectionChanged("completed", _ => eval.Reevaluate());   // only for the action-writes-the-set path
  ```

> **Do not combine them.** `MarkCompleted` already re-derives, so also wiring the `OnCollectionChanged` bridge
> would double-evaluate. Use `MarkCompleted` **or** the action+bridge — never both.

A Linear edge can *also* gate directly on the same set with `CollectionCountAtLeastCondition { completed, 2 }`.
(A turnkey wrapper that owns the evaluator and auto-bridges is deferred until a real consumer shows it is a
burden — owning the evaluator above is already a few lines.)

---

## Flow engine — `FlowRunner`

Cursor-less and **multi-active**. Firing a node runs its graphcore `OnEnterActions` over the shared context,
emits `OnNodeFired`, then **forks** — delivering a token along *every* outgoing edge whose condition passes.
A node with multiple incoming edges **joins**: it fires once enough distinct incoming edges have delivered a
token (default threshold = its incoming-edge count = AND; per-node configurable for k-of-N / OR). One `Fire`
resolves the whole reachable sub-flow as a synchronous cascade.

```csharp
// cast → {damage, debuff, vfx} → cooldown
foreach (var id in new[] { "cast", "damage", "debuff", "vfx", "cooldown" })
    graph.AddNode(new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId });
graph.AddEdge(new BaseEdgeData { FromNodeId = "cast",   ToNodeId = "damage" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "cast",   ToNodeId = "debuff" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "cast",   ToNodeId = "vfx" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "damage", ToNodeId = "cooldown" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "debuff", ToNodeId = "cooldown" });
graph.AddEdge(new BaseEdgeData { FromNodeId = "vfx",    ToNodeId = "cooldown" });

var flow = new FlowRunner(graph, new BaseContext());
flow.OnNodeFired += id => Debug.Log($"fired: {id}");
flow.Fire("cast");                  // cast, damage, debuff, vfx all fire; cooldown fires once, last
```

### Configuration (constructor)

```csharp
new FlowRunner(graph, context,
    oneShotNodeIds:        new[] { "vfx" },                              // fire at most once until Reset()
    joinThresholds:        new Dictionary<string, int> { ["cd"] = 1 },  // k-of-N per node (default = AND)
    maxFiresPerPropagation: 10000);                                     // cycle safety cap
```

- **Re-pass** is intentional: a non-one-shot node may fire again on a later `Fire`. Cycles are permitted but
  bounded by the fire-count cap — hitting it logs a single `[GraphStandard]` warning instead of looping
  forever. The cascade is driven by an explicit work queue (not recursion), so a deep/wide flow cannot
  overflow the call stack before reaching the cap.
- **One-shot**: a one-shot node fires at most once until `Reset()`, which clears all fired/token state.
- **Robust joins**: each edge gets a stable internal token at construction, independent of `BaseEdgeData.Id`,
  so a graph built in code with empty edge ids still joins correctly.

`Fire(nodeId)` triggers a node directly (bypassing its join threshold). `HasFired(id)` / `FiredNodeIds`
report what fired since the last `Reset`.

---

## Assembly Definitions

| Assembly | Platforms | Auto-referenced |
|----------|-----------|-----------------|
| `com.faolline.graphstandard.Runtime` | All | Yes |
| `com.faolline.graphstandard.Tests.EditMode` | Editor only | No (test-only) |

---

## Constraints

- **graphcore is never modified** — every engine is additive, pure C# over graphcore's public API.
- **Universal abstractions only** — prerequisite/threshold/fork/join/re-pass/one-shot are domain-neutral;
  zero game vocabulary leaks in.
- **Headless** — no `MonoBehaviour`/`UnityEvent`; plain `C# Action<T>`; fully EditMode-testable.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
