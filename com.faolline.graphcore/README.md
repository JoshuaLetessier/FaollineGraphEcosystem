# com.faolline.graphcore

**Version**: 0.33.2 — **Unity**: 6000.x — **C#**: 9 / Roslyn

Shared foundation library for graph-based systems in the Faolline ecosystem. Provides the
**data layer** (graph structure, nodes, edges, parameters) and the **execution runtime**
(headless state machine, context blackboard, pluggable executors, SubGraph nesting, history).

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
│   │   ├── BaseGraph           ScriptableObject container (nodes, edges, parameters, GraphId)
│   │   └── BaseContext         Typed parameter blackboard (bool/int/float/string/Vector2/Vector3/Color)
│   ├── Nodes/
│   │   ├── BaseNodeData        Abstract base for all nodes
│   │   ├── StartNodeData       Graph entry point
│   │   ├── StatementNodeData   Generic statement node
│   │   ├── ChoiceNodeData      Branching node with named choices
│   │   ├── EndNodeData         Terminal node (carries EndReason)
│   │   └── SubGraphNodeData    Delegates execution to a nested BaseGraph
│   ├── Edges/
│   │   └── BaseEdgeData        Directed connection between two nodes (optional condition)
│   ├── Parameters/
│   │   ├── ParameterData       Typed parameter declaration on a graph
│   │   └── ParameterType       Enum: Bool | Int | Float | String | Vector2 | Vector3 | Color
│   ├── Choices/
│   │   └── BaseChoice          Named branch target on a ChoiceNodeData
│   ├── Actions/
│   │   └── BaseAction          ScriptableObject — executed on node enter/exit
│   ├── Conditions/
│   │   └── BaseCondition       ScriptableObject — guards node entry or edge traversal
│   └── Execution/
│       ├── INodeExecutor       Pluggable executor interface (Execute + default-no-op Undo)
│       ├── NodeExecutorRegistry Maps NodeType strings to INodeExecutor instances
│       ├── BaseRunner          Headless state machine — drives graph traversal
│       ├── RunnerState         Idle | NodeReady | Paused | Ended
│       ├── GraphExecutionState One stack frame (graph + current node + context)
│       ├── HistoryEntry        Snapshot for GoBack / GoBackToCheckpoint
│       └── GraphCycleException Thrown on SubGraph cycle detection
│
└── Tests/EditMode/
    ├── DataLayer/              Unit tests for graph structure types
    └── Execution/              Unit tests for BaseContext, BaseRunner, SubGraph, History
```

---

## Data Layer

### BaseGraph

`ScriptableObject` container asset. Create via `Assets > Create > GraphCore > Base Graph`.

| Member | Description |
|--------|-------------|
| `GraphId` | Stable GUID, assigned once on `OnEnable`, never overwritten |
| `Nodes` | `IReadOnlyList<BaseNodeData>` |
| `Edges` | `IReadOnlyList<BaseEdgeData>` |
| `Parameters` | `IReadOnlyList<ParameterData>` — declared parameters for `BaseContext.InitFromGraph` |
| `EntryNodeId` | Id of the node where execution starts |
| `HistoryDepth` | Max history entries (default: 20; 0 = unlimited) |
| `AddNode / AddEdge / AddParameter` | Mutation helpers (use from tooling only) |

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
| `AwaitSignalName` | When set, entering the node **parks** the runner until `RaiseSignal(name)` is raised (0.4.0) |
| `ResumeConditions` | `List<BaseCondition>` — optional gate a matching await-signal must pass to resume; empty = none. A failing gate ignores the raise and keeps the node parked (re-armable) (0.7.0) |
| `WaitDuration` | When `> 0`, entering the node holds for this many seconds of host-fed time via `Tick` before advancing (0.6.0) |

`AwaitSignalName` and `WaitDuration` are append-only universal metadata on every node — they make graphs
*wait*: on an external cue (a signal) or on elapsed time. See **Signals & timed waits** under the runner.

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

Typed parameter blackboard. No Unity lifecycle dependency.

```csharp
var ctx = new BaseContext();

// Read / write
ctx.Set<int>("Score", 0);
int score = ctx.Get<int>("Score");        // throws KeyNotFoundException if absent
bool ok   = ctx.TryGet<int>("Score", out int v);
bool has  = ctx.Has("Score");

// Supported types: bool, int, float, string, Vector2, Vector3, Color
// Unsupported types (object/GameObject references) throw ArgumentException on Set<T>

// Change notifications (per key)
ctx.OnParameterChanged("Score", val => Debug.Log($"Score: {val}"));
ctx.OffParameterChanged("Score", handler);

// Initialize from graph's declared ParameterData defaults
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
(`false` — fresh `BaseContext` initialized from the sub-graph's declared parameters).

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
`MonoBehaviour`; the host (e.g. a driver) decides when to feed signals and time.

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

## Context: parameters, signals, collections, scopes

`BaseContext` is more than a typed blackboard:

- **Parameters** — `Set/Get/TryGet/Has` for `bool`/`int`/`float`/`string`/`Vector2`/`Vector3`/`Color`, with `OnParameterChanged`.
- **Signals** — `RaiseSignal(name[, payload])`, `OnSignal`/`OffSignal`, `TryGetLastSignal` (0.4.0),
  `HasSignalBeenRaised`/`ForgetSignal` (durable history, 0.22.0), and `OnAnySignalRaised`/`OffAnySignalRaised`
  (wildcard, fires after per-name handlers, 0.23.0).
- **Collections** (0.4.0) — named string-sets for save-friendly state (inventory, visited rooms, a
  completed-set): `AddToCollection`/`RemoveFromCollection`/`CollectionContains`/`CollectionCount`/
  `GetCollection`/`ClearCollection`/`OnCollectionChanged`/`GetAllCollections`. Deep-copied by `DeepClone`.
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

## Assembly Definitions

| Assembly | Platforms | Auto-referenced |
|----------|-----------|-----------------|
| `com.faolline.graphcore.Runtime` | All | Yes |
| `com.faolline.graphcore.Tests.EditMode` | Editor only | No (test-only) |

---

## Test Coverage

111 EditMode tests across two layers:

| Suite | File | Coverage |
|-------|------|----------|
| Data layer structure | `DataLayer/` (8 files) | Nodes, edges, conditions, actions, choices, parameters, graph |
| BaseContext blackboard | `Execution/BaseContextTests.cs` | Set/Get/TryGet, subscriptions, DeepClone, InitFromGraph |
| Executor registry | `Execution/NodeExecutorRegistryTests.cs` | Registration, resolution, default Undo |
| BaseRunner linear | `Execution/BaseRunnerLinearTests.cs` | Start, Proceed, entry/exit actions, EntryConditions, ChooseById |
| BaseRunner SubGraph | `Execution/BaseRunnerSubGraphTests.cs` | Push/pop, context isolation, cycle detection, nested depth |
| BaseRunner history | `Execution/BaseRunnerHistoryTests.cs` | GoBack, GoBackToCheckpoint, depth cap, unlimited |

---

## Changelog

### 0.21.0
- **Composite conditions**: `AndCondition`, `OrCondition`, `NotCondition` — nest arbitrarily to build
  complex gates from simple building blocks.
- **Param-to-param comparison**: `IntCompareCondition`, `FloatCompareCondition`, `StringCompareCondition` —
  compare two context parameters against each other (not just a parameter vs. a constant).
- **New actions**: `RaiseSignalAction` (fire a named signal from a node action), `ToggleBoolAction`
  (flip a bool parameter), `SetRandomIntAction` (set an int parameter to a random value in a range).
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
- Added `BaseContext` — typed parameter blackboard with subscriptions, deep clone, graph init
- Added `INodeExecutor` / `NodeExecutorRegistry` — pluggable executor dispatch
- Added `BaseRunner` — headless state machine with SubGraph stack, cycle detection, history rewind

### 0.1.0
- Initial release: data layer (graph, nodes, edges, parameters, actions, conditions, choices)
