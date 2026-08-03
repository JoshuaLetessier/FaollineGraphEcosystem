# com.faolline.graphcore

**Version**: 0.38.0 — **Unity**: 6000.x — **C#**: 9 / Roslyn

Shared foundation library for graph-based systems in the Faolline ecosystem. Provides the
**data layer** (graph structure, nodes, edges) and the **execution runtime** (headless state
machine, context blackboard, pluggable executors, SubGraph nesting, history) built around three
sharply distinct **context primitives** — **Variables**, **Signals**, and **Collections**.

---

## Installation

graphcore is the **base package** of the ecosystem. Install it via **Package Manager ▸ + ▸ Add
package from git URL**:

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphcore#master
```

Then open **Window ▸ Faolline ▸ Graph Ecosystem Modules** to add the other packages (Graph
Localization, Graph Dialogue System) with one click — dependencies are resolved automatically. Pin
`#master` to a tag (e.g. `#graphcore-v0.2.0`) for reproducible installs.

See [`../INSTALL.md`](../INSTALL.md) for the full install guide.

---

## Architecture

```
com.faolline.graphcore
│
├── Runtime/
│   ├── Graph/
│   │   ├── BaseGraph           ScriptableObject container (nodes, edges, GraphId)
│   │   └── BaseContext         Typed blackboard: variables + signals + collections + scopes
│   ├── Nodes/
│   │   ├── BaseNodeData        Abstract base for all nodes
│   │   ├── StartNodeData       Graph entry point
│   │   ├── StatementNodeData   Generic statement node
│   │   ├── ChoiceNodeData      Branching node with named choices
│   │   ├── EndNodeData         Terminal node (carries EndReason)
│   │   ├── SubGraphNodeData    Delegates execution to a nested BaseGraph
│   │   └── GraphLinkNodeData   Non-executing documentary cross-reference
│   ├── Edges/
│   │   └── BaseEdgeData        Directed connection between two nodes (optional condition)
│   ├── Variables/
│   │   ├── VariableDef             Stable-GUID typed variable asset (identity + type + default)
│   │   ├── VariableType             Enum: Bool | Int | Float | String | Vector2 | Vector3 | Color
│   │   ├── IVariableReferencing     Contract: "this action/condition reads/writes these VariableDefs"
│   │   └── GraphVariableScanner     Walks a graph's actions/conditions to discover referenced variables
│   ├── Signals/
│   │   └── SignalDef               Stable-GUID signal asset (wake-event + durable latch identity)
│   ├── Collections/
│   │   ├── CollectionDef           Stable-GUID collection-key asset
│   │   └── CollectionEntry         Stable-GUID collection-item asset
│   ├── Grouping/
│   │   └── GraphCategoryGroup   Label + list of BaseGraph — no-code, multi-membership organizational asset
│   ├── Choices/
│   │   └── BaseChoice          Named branch target on a ChoiceNodeData
│   ├── Actions/
│   │   └── BaseAction          ScriptableObject — executed on node enter/exit
│   ├── Conditions/
│   │   └── BaseCondition       ScriptableObject — guards node entry or edge traversal
│   ├── IStableGuidIdentity     Interface shared by BaseGraph/VariableDef/SignalDef/CollectionDef/CollectionEntry
│   ├── StableGuidPersistence   Editor-only GUID-to-disk flush (auto-heal + PersistAll for CI)
│   └── Execution/
│       ├── INodeExecutor       Pluggable executor interface (Execute + default-no-op Undo)
│       ├── NodeExecutorRegistry Maps NodeType strings to INodeExecutor instances
│       ├── BaseRunner          Headless state machine — drives graph traversal
│       ├── RunnerState         Idle | NodeReady | Paused | Ended | WaitingForSignal | WaitingForTime
│       ├── GraphExecutionState One stack frame (graph + current node + context)
│       ├── HistoryEntry        Snapshot for GoBack / GoBackToCheckpoint
│       └── GraphCycleException Thrown on SubGraph cycle detection
│
├── Editor/Tools/
│   ├── GraphValidator                Structural + type-safety lint (menu: Validate Selected Graph)
│   ├── SignalConstantsGenerator      → GraphSignals class    (menu: Signals ▸ Generate Constants)
│   ├── VariableConstantsGenerator    → GraphVariables class  (menu: Variables ▸ Generate Constants)
│   └── ConstantsGeneratorCore        Shared sanitize/collision core for both generators
│
├── Editor/Registry/
│   └── InspectorExtensionRegistry    Seam for downstream libs to inject graph/node inspector UI — see EXTENSIBILITY.md
│
├── Editor/Grouping/
│   └── GraphCategoryGroupInspectorExtension   Worked example consumer of the registry above
│
└── Tests/EditMode/
    ├── DataLayer/              Unit tests for graph structure types
    └── Execution/              Unit tests for BaseContext, BaseRunner, SubGraph, History
```

---

## The three context primitives

Everything a running graph reads or writes lives on `BaseContext`, split into three primitives
chosen by **what capability the data needs** — not by what shape it happens to be stored in. Mixing
them up is the single most common early-authoring mistake, so here they are side by side:

| | **Variable** | **Signal** | **Collection** |
|---|---|---|---|
| **What it is** | a durable, typed value that changes over time (hp, score, a flag) | a transient wake-event + a durable "has this ever fired" latch | a durable named set of items, each with a quantity (inventory, visited rooms) |
| **Governed asset** | `VariableDef` | `SignalDef` | `CollectionDef` (the set's key) + `CollectionEntry` (an item) |
| **Carries a type?** | yes — `VariableType` (Bool/Int/Float/String/Vector2/Vector3/Color) | no | items are opaque GUID strings |
| **Carries a default?** | yes, typed | no | no |
| **Read/write API** | `Set<T>` / `Get<T>` / `TryGet<T>` / `Has` | `RaiseSignal` / `OnSignal` / `HasSignalBeenRaised` / `ForgetSignal` | `AddToCollection` / `RemoveFromCollection` / `CollectionContains` / `CollectionCount` / `…ItemCount` |
| **Can wake a parked node?** | **no** | **yes** — the only primitive `BaseNodeData.AwaitSignalName` can wait on | no |
| **Notifications** | `OnVariableChanged` + `OnAnyVariableChanged` | `OnSignal` + `OnAnySignalRaised` | `OnCollectionChanged` + `OnAnyCollectionChanged` |
| **Declaration** | declaration-free — `InitFromGraph` **discovers** every `VariableDef` a graph's actions/conditions reference (via `IVariableReferencing`) and seeds its default | none — a signal exists the moment it is first raised or awaited | none — a collection exists the moment its first item is added |
| **Generated code constants** | `GraphVariables` (menu `Faolline ▸ Variables ▸ Generate Constants`) | `GraphSignals` (menu `Faolline ▸ Signals ▸ Generate Constants`) | *(none yet)* |

**Why the split is load-bearing, not incidental**: a Variable is a *quiet* write (nothing reacts unless
something explicitly subscribed), a Signal is a *waking* write (it can pull a parked node out of
`WaitingForSignal`). Folding them into one "typed signal" primitive would have to re-invent that
quiet-vs-waking distinction from scratch — so graphcore keeps them separate on purpose. If a graph
needs to resume when a **variable** crosses a threshold (e.g. "continue when hp ≤ 0"), combine the
two: bridge the variable's `OnVariableChanged`/`OnAnyVariableChanged` into a `RaiseSignal`, and let the
awaiting node's `ResumeConditions` re-check the variable each time the signal fires. See
[Authoring patterns](#authoring-patterns).

### Identity: stable GUID assets, not strings

`VariableDef`, `SignalDef`, `CollectionDef`, and `CollectionEntry` — plus `BaseGraph` itself — all
implement `IStableGuidIdentity`: a GUID assigned once in `OnEnable`, never editable, persisted to
disk (`StableGuidPersistence`) and covered by the editor's duplicate-GUID detector. The asset's
**display name is purely cosmetic** — renaming it (or the file) never changes the identity, so
renaming is *free*:

```csharp
VariableDef hp = /* your Hp.asset */;
context.Set<int>(hp, 100);          // keys on hp.Key (the GUID), via an implicit string conversion
context.Get<int>(hp);               // same GUID — survives renaming DisplayName or the asset file
```

**Islands**: an asset (`VariableDef`/`SignalDef`/`CollectionDef`) keys on its GUID; the raw-string
API (`context.Set<int>("hp", …)`, `RaiseSignal("advance")`) keys on the literal you typed. **The two
never cross** — a raw `RaiseSignal("advance")` does not wake a node awaiting the `SignalDef` asset
named "advance", and a raw `Set<int>("hp", …)` does not feed a condition reading the `VariableDef`
asset. This is deliberate: the raw channel is the escape-hatch for dynamic/code-first/quick use; the
asset channel is the compile-checked, rename-safe, drag-and-drop authoring surface. Pick one per key
and stay on it — don't mix.

To reference an asset from pure host code (no held reference), generate constants:

```csharp
// Faolline ▸ Variables ▸ Generate Constants  →  Assets/Generated/GraphVariables.cs
context.Set<int>(GraphVariables.Hp, 100);     // GraphVariables.Hp's VALUE is the VariableDef's GUID

// Faolline ▸ Signals ▸ Generate Constants    →  Assets/Generated/GraphSignals.cs
context.RaiseSignal(GraphSignals.BossDefeated);
```

The generated **symbol** comes from the asset's `DisplayName` (renaming it regenerates a new symbol,
breaking stale code loudly at compile — the intended, safe rename); the generated **value** is the
GUID (never changes, so saves/awaits/comparisons keep matching across the rename).

### Type-safety (Variables only)

A `VariableDef` carries a `VariableType`; every action/condition that references one implements
`IVariableReferencing`, tagging its reference with the type it expects. `GraphValidator` cross-checks
this — wiring a `SetIntAction` to a `Float`-typed `VariableDef` (or two differently-typed actions to
the same `VariableDef`) is a validator **error**, caught at authoring time instead of silently
corrupting the stored value at runtime.

---

## Data Layer

### BaseGraph

`ScriptableObject` container asset. Create via `Assets > Create > GraphCore > Base Graph`.

| Member | Description |
|--------|-------------|
| `GraphId` | Stable GUID, assigned once on `OnEnable`, never overwritten |
| `Nodes` | `IReadOnlyList<BaseNodeData>` |
| `Edges` | `IReadOnlyList<BaseEdgeData>` |
| `EntryNodeId` | Id of the node where execution starts |
| `HistoryDepth` | Max history entries (default: 20; 0 = unlimited) |
| `AddNode / AddEdge` | Mutation helpers (use from tooling only) |

There is **no declared parameter/variable list on the graph** — a graph's variables are whichever
`VariableDef` assets its actions/conditions reference; see
[The three context primitives](#the-three-context-primitives).

Create a graph programmatically:

```csharp
BaseGraph graph = ScriptableObject.CreateInstance<BaseGraph>();
graph.EntryNodeId = "start";
graph.AddNode(new StartNodeData    { Id = "start", NodeType = StartNodeData.NodeTypeId });
graph.AddNode(new EndNodeData      { Id = "end",   NodeType = EndNodeData.NodeTypeId,
                                     EndReason = EndReason.Completed });
graph.AddEdge(new BaseEdgeData     { Id = "e0", FromNodeId = "start", ToNodeId = "end" });
```

### Nodes

All nodes derive from `BaseNodeData`. Key members:

| Member | Description |
|--------|-------------|
| `Id` | Unique string id within the graph |
| `NodeType` | Constant string used for executor dispatch |
| `Title` | Optional author-facing name (shown in the editor; falls back to the type label) |
| `EntryConditions` | `List<BaseCondition>` — all must pass to enter the node |
| `OnEnterActions` | `List<BaseAction>` — run after conditions pass |
| `OnExitActions` | `List<BaseAction>` — run before advancing |
| `IsCheckpoint` | If `true`, `GoBackToCheckpoint` can restore to this node |
| `AwaitSignalName` / `AwaitSignals` | When set, entering the node **parks** the runner until a matching `SignalDef`/raw signal is raised (0.4.0; asset list 0.26.0) — the **only** primitive that can resume a parked node |
| `ResumeConditions` | `List<BaseCondition>` — optional gate a matching await-signal must pass to resume; empty = none. A failing gate ignores the raise and keeps the node parked (re-armable) (0.7.0) |
| `WaitDuration` | When `> 0`, entering the node holds for this many seconds of host-fed time via `Tick` before advancing (0.6.0) |

`AwaitSignalName`/`AwaitSignals` and `WaitDuration` are append-only universal metadata on every
node — they make graphs *wait*: on an external cue (a Signal — never a Variable or Collection change
directly) or on elapsed time. See **Signals & timed waits** under the runner.

Built-in node types and their `NodeTypeId` constants:

| Type | NodeTypeId |
|------|-----------|
| `StartNodeData` | `"graphcore/start"` |
| `StatementNodeData` | `"graphcore/statement"` |
| `ChoiceNodeData` | `"graphcore/choice"` |
| `EndNodeData` | `"graphcore/end"` |
| `SubGraphNodeData` | `"graphcore/subgraph"` |
| `GraphLinkNodeData` | `"graphcore/graph-link"` |

### GraphLink — documentary cross-reference (non-executing)

`GraphLinkNodeData` references another `BaseGraph` (`TargetGraph`, any kind) purely as **authoring
documentation** — e.g. annotate a zone's flow with the quests that belong to it. Unlike `SubGraphNodeData`
(which is *executed/traversed*), a GraphLink is **never run**: if it is ever wired onto the execution path the
runner passes straight through it (no pause, no actions, no executor, no access to the target). It renders as a
distinct "📎 Kind: Name" node in every lib editor, and **double-clicking it opens the target** in the proper
editor via `GraphEditorWindowRegistry` (each lib editor registers its window; missing/unregistered → the asset
is selected/pinged with a `[GraphCore]` diagnostic). See `specs/030-graphlink-navigation/quickstart.md`.

**Soft reference, since 0.41.0.** `TargetGraph` is backed by a GUID (`TargetGraphGuid`), not a hard
`BaseGraph` field — the target is never pulled into the owning graph's build/asset-bundle dependency
closure, which used to happen for zero runtime benefit since this node is never touched at runtime. The
`TargetGraph` property itself is unchanged (still a real `ObjectField`, still drag-and-drop, still
navigable) and is Editor-only (`#if UNITY_EDITOR`) since nothing at runtime ever legally dereferences it.
`GraphValidator` flags a GraphLink whose GUID no longer resolves to any asset. See
`specs/047-graph-soft-links/`.

### BaseEdgeData

```csharp
public class BaseEdgeData
{
    public string Id;
    public string FromNodeId;
    public string ToNodeId;
    public string PortName;          // used with ChooseById for named choices
    public BaseCondition Condition;  // null = unconditional
    public List<Vector2> Waypoints;  // editor-only bend points (orthogonal routing); no runtime effect (0.8.0)
}
```

**Malleable edges (editor, 0.8.0)**: edges render as right-angle polylines you can shape — **double-click** an
edge to add a bend point, **drag** the dots to move them, **right-click** a dot to remove it. Bends live in
`Waypoints` (editor metadata, like a node's `Position`; persisted, no runtime effect). The live preview can lag
the data while editing; **Save (Ctrl+S)** fully refreshes the routing (a toolbar hint notes this). Since 0.11.0
edges also **route around node boxes** (live, recomputed each repaint) instead of passing under them.

**Auto-arrange (editor, 0.8.0)**: the toolbar **Arrange** button lays the graph out left-to-right in tidy
layers (longest-path layering, crossing reduction, cycle-safe) and routes column-skipping edges through a lane
below the rows so they don't pass under nodes. It clears manual bends (a fresh layout) and frames the result.

**Window persistence & auto-save (editor, 0.9.0)**: the open graph survives a domain reload (entering Play, a
recompile, or reopening Unity) — the window reloads it into the rebuilt view instead of coming back blank. The
canvas auto-saves before the window/editor closes and before a reload, so node/group moves (synced into the data
only on save) aren't lost. The viewport (zoom/pan) is not yet persisted.

**Live in-game run cursor (editor, 0.10.0)**: while playing, the window highlights the running graph like the
Animator window — a per-node state map (live cursor pulsing, visited trail, sub-graph parents, end). It reads a
zero-footprint editor-only seam (`GraphRunMonitor` + `IGraphRunProbe`); `BaseRunner` self-registers a probe, so
any host (gameflow, dialogue, custom) lights up for free, and the graphstandard Reactive/Flow engines register
their own (Locked/Available/Completed). Compiled out of player builds.

---

## Execution Runtime

### BaseContext

Typed blackboard for the three context primitives (Variables / Signals / Collections — see
[above](#the-three-context-primitives)). No Unity lifecycle dependency.

```csharp
var ctx = new BaseContext();

// Variables — raw-string (islands escape hatch)
ctx.Set<int>("Score", 0);
int score = ctx.Get<int>("Score");        // throws KeyNotFoundException if absent
bool ok   = ctx.TryGet<int>("Score", out int v);
bool has  = ctx.Has("Score");

// Variables — governed asset (VariableDef implicitly converts to its GUID string)
ctx.Set<int>(hpVariableDef, 100);
ctx.Get<int>(hpVariableDef);

// Supported types: bool, int, float, string, Vector2, Vector3, Color
// Unsupported types (object/GameObject references) throw ArgumentException on Set<T>

// Change notifications (per key, either channel)
ctx.OnVariableChanged("Score", val => Debug.Log($"Score: {val}"));
ctx.OffVariableChanged("Score", handler);
ctx.OnAnyVariableChanged(key => Debug.Log($"{key} changed"));

// Seed every VariableDef the graph's actions/conditions reference (declaration-free — no per-graph list)
ctx.InitFromGraph(graph);

// Deep-clone (values only, no subscribers)
BaseContext snapshot = ctx.DeepClone();
```

**Subclassing**: override `CreateCloneInstance()` and `DeepClone()` to carry additional fields through history snapshots:

```csharp
public class DialogueContext : BaseContext
{
    public string CurrentSpeaker { get; set; }

    protected override BaseContext CreateCloneInstance() => new DialogueContext();

    public override BaseContext DeepClone()
    {
        var clone = (DialogueContext)base.DeepClone();
        clone.CurrentSpeaker = CurrentSpeaker;
        return clone;
    }
}
```

### INodeExecutor

Register one per `NodeTypeId` to provide execution logic.

```csharp
public class StatementExecutor : INodeExecutor
{
    public string NodeType => StatementNodeData.NodeTypeId;

    public void Execute(BaseNodeData node, BaseContext context)
    {
        // type-specific logic
    }

    // Undo: default no-op. Override for reversible side-effects.
    public void Undo(BaseNodeData node, BaseContext context)
    {
        // undo side-effects for GoBack
    }
}
```

### NodeExecutorRegistry

```csharp
var registry = new NodeExecutorRegistry();
registry.Register(new StatementExecutor());   // silently replaces on duplicate type
registry.Register(new MyDialogueExecutor());

INodeExecutor ex = registry.GetExecutor("graphcore/statement"); // null if not registered
```

### BaseRunner

Headless state machine. No `MonoBehaviour`, no `UnityEvent` — plain `C# Action<T>`.

**State machine:**

```
Idle ──Start()──► NodeReady ──Proceed() / ChooseById()──► ... ──► Ended
                      ▲                                               │
                      └──────────────GoBack()──────────────────────┘
```

**Events:**

| Event | When fired |
|-------|-----------|
| `OnNodeEntered(BaseNodeData)` | After conditions pass, enter-actions run, executor called |
| `OnNodeCompleted(BaseNodeData)` | Immediately after `OnNodeEntered` — runner pauses here |
| `OnEnded(EndReason)` | When an `EndNodeData` is reached at root level |
| `OnStuck()` | When an entry condition fails or no outgoing edge is available |
| `OnWaitingForSignal(BaseNodeData, string)` | The node declared `AwaitSignalName`; the runner parks (0.4.0) |
| `OnWaitingForTime(BaseNodeData, float)` | The node declared `WaitDuration`; the runner holds on time (0.6.0) |

`RunnerState` is `Idle | NodeReady | Paused | Ended | WaitingForSignal | WaitingForTime`.

**Node execution sequence** (per node):

1. Evaluate `EntryConditions` → fail: raise `OnStuck`, stay `NodeReady`
2. Run `OnEnterActions`
3. Call `INodeExecutor.Execute` (if registered)
4. Raise `OnNodeEntered`, then `OnNodeCompleted` — **runner pauses here**
5. *(on Proceed / ChooseById)* Run `OnExitActions`
6. Evaluate outgoing edges, append history snapshot
7. Advance to next node

**Linear execution:**

```csharp
var runner = new BaseRunner();

runner.OnNodeCompleted += _ => runner.Proceed(); // auto-advance
runner.OnEnded += reason => Debug.Log($"Ended: {reason}");
runner.OnStuck += () => Debug.LogWarning("Stuck — no valid edge");

runner.Start(graph, context, registry);
```

**Choices:**

```csharp
runner.OnNodeCompleted += node =>
{
    if (node is ChoiceNodeData choice)
        runner.ChooseById(choice.Choices[playerIndex].Id);
    else
        runner.Proceed();
};
```

**SubGraph nesting:**

When `BaseRunner` encounters a `SubGraphNodeData` it pushes a new stack frame and enters
the sub-graph. On `EndNodeData` inside the sub-graph, the frame is popped and the parent
resumes automatically. Context is either shared (`InheritParentContext = true`) or isolated
(`false` — fresh `BaseContext`, seeded from whichever `VariableDef`s the sub-graph's own
actions/conditions reference).

Cycle detection is automatic: if the sub-graph's `GraphId` is already on the stack,
`GraphCycleException` is thrown.

```csharp
try { runner.Start(graph, ctx, registry); }
catch (GraphCycleException ex) { Debug.LogError(ex.CyclicGraphId); }
```

**History:**

```csharp
runner.GoBack();                // restore previous snapshot (one step)
runner.GoBackToCheckpoint();    // restore nearest node where IsCheckpoint == true
```

History depth is controlled by `BaseGraph.HistoryDepth` (default 20, 0 = unlimited).
Snapshots are taken on each transition; `GoBack` calls `INodeExecutor.Undo` on the
current node before restoring.

---

## Signals & timed waits

graphcore graphs can **wait** — for an external cue or for elapsed time — and the host drives both. No
`MonoBehaviour`; the host (e.g. a driver) decides when to feed signals and time. Only a **Signal**
(never a Variable or a Collection change) can resume a parked node — see
[The three context primitives](#the-three-context-primitives) for why that split is deliberate.

**Signals** (0.4.0): set `BaseNodeData.AwaitSignalName` to park the runner on entry. The host raises a
signal; if the current node awaits exactly that name, the runner advances as `Proceed` would. Delivery to
context subscribers happens whether or not anything is waiting.

```csharp
gate.AwaitSignalName = "advance";          // entering 'gate' parks the runner (State = WaitingForSignal)
// … later, from the host:
runner.RaiseSignal("advance");             // matches → resumes; non-matching names are ignored
runner.RaiseSignal<int>("score", 10);      // scalar payload, readable via context.TryGetLastSignal

// context-level signal channel (decoupled listeners):
context.OnSignal("advance", args => { /* args.Name, args.GetPayload<T>() */ });
```

**Guarded await — re-armable resume gate** (0.7.0): an await node may carry optional `ResumeConditions`
(universal `BaseCondition`s, AND, null entries skipped). A matching signal resumes the node **only if the gate
passes**; if it fails, the raise is **ignored and the node stays parked** — re-armable, so the actor can raise
again once the world is ready. Empty (the default) = resume on name match alone (unchanged). This expresses
"press the button anytime, it only acts when the world is ready" *in the graph*, with no host glue — the key
difference from gating an outgoing edge (which would consume the signal and leave the node stuck on a false
gate). A direct host `Advance`/GoTo override is **not** gated.

```csharp
exitNode.AwaitSignalName = "exit";
exitNode.ResumeConditions.Add(twoOfThreeDone);   // any BaseCondition over the context
runner.RaiseSignal("exit");                       // ignored until the gate passes; then resumes
```

**Timed waits** (0.6.0): set `BaseNodeData.WaitDuration` (seconds) to hold on entry until the host feeds
enough time. The runner owns no clock — the host calls `Tick`:

```csharp
wait.WaitDuration = 2f;                     // entering 'wait' holds (State = WaitingForTime)
runner.Tick(Time.deltaTime);               // each frame; advances once the duration elapses. dt ≤ 0 ignored.
```

If a node sets both, the signal wait takes precedence. `StartFrom(graph, nodeId, ctx, registry)` starts at a
given node (e.g. restoring a saved session) instead of the entry node.

## Context API reference

Quick reference for the full `BaseContext` surface, grouped by primitive (see
[The three context primitives](#the-three-context-primitives) for the conceptual model and the
identity/islands rules):

- **Variables** — `Set<T>`/`Get<T>`/`TryGet<T>`/`Has` for `bool`/`int`/`float`/`string`/`Vector2`/`Vector3`/`Color`,
  with `OnVariableChanged`/`OffVariableChanged`/`OnAnyVariableChanged`/`OffAnyVariableChanged`, and
  `GetAllVariables` (snapshot for serialization). Governed via `VariableDef` or raw-string (islands).
- **Signals** — `RaiseSignal(name[, payload])`, `OnSignal`/`OffSignal`, `TryGetLastSignal` (0.4.0),
  `HasSignalBeenRaised`/`ForgetSignal` (durable history, 0.22.0), and `OnAnySignalRaised`/`OffAnySignalRaised`
  (wildcard, fires after per-name handlers, 0.23.0). Governed via `SignalDef` or raw-string (islands).
- **Collections** (0.4.0; ordered + quantities since 0.31.0) — named sets for save-friendly state (inventory,
  visited rooms, a completed-set): `AddToCollection`/`RemoveFromCollection` (plain or with a `count` for
  stacking), `CollectionContains`/`CollectionCount`/`CollectionItemCount`, `GetCollection`/
  `GetCollectionWithCounts`, `ClearCollection`, `OnCollectionChanged`/`OnAnyCollectionChanged`,
  `GetAllCollections`. Deep-copied by `DeepClone`. Governed via `CollectionDef`/`CollectionEntry` or
  raw-string (islands).
- **Scoped (global + local) contexts** (0.3.0) — a sub-graph can ride the parent context with a fresh
  **local overlay** (`BeginLocalContext`/`EndLocalContext`); reads fall through to global, writes land local
  and are discarded when the scope ends. Used by `SubGraphNodeData.OpensScope`.

## Authoring patterns

A few idioms the runtime and the `GraphValidator` (Editor) are built around — following them keeps graphs
readable and lets the validator catch mistakes before play.

### Default / "else" branch = an unconditioned edge, placed last

An auto-advanced node (anything but a choice node) leaves through the **first outgoing edge whose condition
passes**. An edge with no condition always passes, so:

- To add a fallback branch, give it **no condition** and make it the **last** outgoing edge — it runs only
  when every earlier (conditioned) branch failed. That is the supported "else"/default.
- An unconditioned edge that is **not** last makes every branch after it unreachable. `GraphValidator` warns
  about this ("…branch(es) after it are unreachable…").
- If two conditioned branches can be true at once, the first still wins; make sibling conditions mutually
  exclusive (`AndCondition`/`NotCondition`) or rely on the ordered default. A router that resolves >1 branch
  logs a warning at runtime.

(Choice nodes are different: their edges are picked by port id via `ChooseById`, so edge order does not matter.)

### Graph-driven gameplay UI (the signal seam)

Keep the graph the source of truth for **when** a piece of gameplay UI appears, without the graph depending on
the UI. Put a `RaiseSignalAction` on the node that should trigger it; the consumer subscribes on the shared
context and reveals its own UI:

```csharp
// Authoring: the "Play a round?" node's OnEnter raises a RaiseSignalAction("StartDiceGame").
// Consumer (a MonoBehaviour, never referenced by the graph):
context.OnSignal("StartDiceGame", _ => dicePanel.Show());
```

The panel is inert until the flow reaches that node. The graph carries **intent** (a named signal); the
consumer owns the **presentation** — the same separation the dialogue/quest libs use. Pair with
`SignalRaisedCondition` if a later branch should gate on "the round was played", and note that a
`QuestEvaluator` with `EnableAutoEvaluate()` now re-derives on raised signals (0.23.0).

### Resuming a node on a Variable threshold ("await hp ≤ 0")

Only a Signal can resume a parked node (see [The three context primitives](#the-three-context-primitives)),
but combining Variables with Signals gets you a fully general condition-await with no new primitive needed.
Bridge once, host-side:

```csharp
context.OnAnyVariableChanged(_ => context.RaiseSignal("Recheck"));   // generic "something changed" tick
```

…and give the parked node a `ResumeCondition` instead of relying on the signal name alone:

```csharp
node.AwaitSignalName = "Recheck";
node.ResumeConditions.Add(hpDepletedCondition);   // e.g. IntCompareCondition(hp <= 0)
```

The node stays parked through every `Recheck` until the condition actually passes — a declarative
condition-await assembled from the two existing primitives, no host-side polling loop required.

## Assembly Definitions

| Assembly | Platforms | Auto-referenced |
|----------|-----------|-----------------|
| `com.faolline.graphcore.Runtime` | All | Yes |
| `com.faolline.graphcore.Tests.EditMode` | Editor only | No (test-only) |

---

## Test Coverage

EditMode tests across the data layer, the context primitives, and the execution runtime (part of the
whole ecosystem's 1118-test green suite):

| Suite | Location | Coverage |
|-------|------|----------|
| Data layer structure | `Tests/EditMode/DataLayer/` | Nodes, edges, conditions, actions, choices, graph, `VariableDef` identity |
| Context primitives | `Tests/EditMode/` (`DesignerActionTests`, `ParamCompareConditionTests`, `Primitive*Tests`, `CompositeConditionTests`) | Variables (governed + raw), Signals, composite conditions |
| BaseContext blackboard | `Tests/EditMode/Execution/` | Set/Get/TryGet, subscriptions, DeepClone, `InitFromGraph` scan-seeding, scoped overlays |
| Executor registry | `Tests/EditMode/Execution/NodeExecutorRegistryTests.cs` | Registration, resolution, default Undo |
| BaseRunner linear | `Tests/EditMode/Execution/BaseRunnerLinearTests.cs` | Start, Proceed, entry/exit actions, EntryConditions, ChooseById |
| BaseRunner SubGraph | `Tests/EditMode/Execution/BaseRunnerSubGraphTests.cs` | Push/pop, context isolation, cycle detection, nested depth |
| BaseRunner history | `Tests/EditMode/Execution/BaseRunnerHistoryTests.cs` | GoBack, GoBackToCheckpoint, depth cap, unlimited |
| Editor tooling | `Tests/EditMode/Editor/` | `GraphValidator` (structural + type-mismatch), `SignalConstantsGenerator`/codegen core |

---

## Changelog

Full history in [`CHANGELOG.md`](CHANGELOG.md). Highlights:

### 0.37.0 — `GraphCategoryGroup` + extensibility doc
A no-code asset (label + list of `BaseGraph`) for organizing any graph into named, possibly-overlapping
groups (e.g. quest "Main"/"Side"), displayed on the graph inspector via a new
`GraphCategoryGroupInspectorExtension` — the first worked example of `InspectorExtensionRegistry` used
to edit a *foreign* asset rather than the inspected graph itself. See [`EXTENSIBILITY.md`](../EXTENSIBILITY.md).

### 0.35.0 — vocabulary rename (no behaviour change)
The three identity assets drop the misleading `Name` suffix (identity is a GUID, not a name), and
"parameter" becomes **"variable"** everywhere: `SignalName`→`SignalDef`, `CollectionName`→`CollectionDef`,
`ParameterName`→`VariableDef`; `GraphParams`→`GraphVariables`; `BaseContext.GetAllParameters`/
`OnParameterChanged`→`GetAllVariables`/`OnVariableChanged`. Renamed via `git mv` keeping each asset's
`.meta` GUID — existing project assets keep their script link.

### 0.34.0 — parameter (now variable) identity re-base
`VariableDef` — a typed, stable-GUID variable asset (spec `033`) — replaces the old raw-string
`_parameterKey` + per-graph declaration list. Declaration-free: `InitFromGraph` discovers every
referenced `VariableDef` via `IVariableReferencing` and seeds its default. Adds the `GraphVariables`
codegen and a validator check for type-mismatched references. Applies the same model spec `032`
proved for signals.

### 0.33.x — stable-GUID persistence hardening
`StableGuidPersistence.ScheduleSave`/`PersistAll` — GUIDs assigned in `OnEnable` are now reliably
flushed to disk (interactive auto-heal + a synchronous CI-safe `PersistAll`).

### 0.26.0 — multi-signal await
`AwaitSignals`/`AwaitSignalNames` — a node can await several signals (logical OR), resuming on the
first one whose `ResumeConditions` pass.

### 0.21.0
- **Composite conditions**: `AndCondition`, `OrCondition`, `NotCondition` — nest arbitrarily to build
  complex gates from simple building blocks.
- **Variable-to-variable comparison**: `IntCompareCondition`, `FloatCompareCondition`, `StringCompareCondition` —
  compare two context variables against each other (not just a variable vs. a constant).
- **New actions**: `RaiseSignalAction` (fire a named signal from a node action), `ToggleBoolAction`
  (flip a bool variable), `SetRandomIntAction` (set an int variable to a random value in a range).
- **Runner signal bridging**: `BaseRunner` now bridges context signals when awaiting, so a signal raised
  on the context while the runner is parked on an await node is delivered to the runner automatically.

### 0.6.0
- **Timed waits**: `BaseNodeData.WaitDuration` + `BaseRunner.Tick` + `RunnerState.WaitingForTime` +
  `OnWaitingForTime`. The host feeds elapsed time; the node holds until the duration elapses.

### 0.4.0
- **Signals**: `BaseNodeData.AwaitSignalName` + `BaseRunner.RaiseSignal`(+payload) +
  `RunnerState.WaitingForSignal` + `OnWaitingForSignal`; a `BaseContext` signal channel
  (`RaiseSignal`/`OnSignal`/`TryGetLastSignal`, `SignalArgs`).
- **Collections**: named string-sets on `BaseContext` (add/remove/contains/count/clear/changed), deep-cloned.

### 0.3.0
- **Global + local execution contexts**: a sub-graph can ride the parent context with a fresh local overlay
  (`BeginLocalContext`/`EndLocalContext`; `SubGraphNodeData.OpensScope`); local writes are discarded on scope
  end. Append-only on `BaseContext`/`BaseRunner`.

### 0.2.0
- Added `BaseContext` — typed blackboard with subscriptions, deep clone, graph init
- Added `INodeExecutor` / `NodeExecutorRegistry` — pluggable executor dispatch
- Added `BaseRunner` — headless state machine with SubGraph stack, cycle detection, history rewind

### 0.1.0
- Initial release: data layer (graph, nodes, edges, actions, conditions, choices)
