# com.faolline.graphcore

**Version**: 0.2.0 — **Unity**: 6000.x — **C#**: 9 / Roslyn

Shared foundation library for graph-based systems in the Faolline ecosystem. Provides the
**data layer** (graph structure, nodes, edges, parameters) and the **execution runtime**
(headless state machine, context blackboard, pluggable executors, SubGraph nesting, history).

---

## Installation

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.faolline.graphcore": "file:../Assets/FaollineGraphEcosystem/com.faolline.graphcore"
  }
}
```

---

## Architecture

```
com.faolline.graphcore
│
├── Runtime/
│   ├── Graph/
│   │   ├── BaseGraph           ScriptableObject container (nodes, edges, parameters, GraphId)
│   │   └── BaseContext         Typed parameter blackboard (bool/int/float/string)
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
│   │   └── ParameterType       Enum: Bool | Int | Float | String
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
| `EntryConditions` | `List<BaseCondition>` — all must pass to enter the node |
| `OnEnterActions` | `List<BaseAction>` — run after conditions pass |
| `OnExitActions` | `List<BaseAction>` — run before advancing |
| `IsCheckpoint` | If `true`, `GoBackToCheckpoint` can restore to this node |

Built-in node types and their `NodeTypeId` constants:

| Type | NodeTypeId |
|------|-----------|
| `StartNodeData` | `"graphcore/start"` |
| `StatementNodeData` | `"graphcore/statement"` |
| `ChoiceNodeData` | `"graphcore/choice"` |
| `EndNodeData` | `"graphcore/end"` |
| `SubGraphNodeData` | `"graphcore/subgraph"` |

### BaseEdgeData

```csharp
public class BaseEdgeData
{
    public string Id;
    public string FromNodeId;
    public string ToNodeId;
    public string PortName;       // used with ChooseById for named choices
    public BaseCondition Condition; // null = unconditional
}
```

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

// Supported types: bool, int, float, string
// Unsupported types throw ArgumentException on Set<T>

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

### 0.2.0
- Added `BaseContext` — typed parameter blackboard with subscriptions, deep clone, graph init
- Added `INodeExecutor` / `NodeExecutorRegistry` — pluggable executor dispatch
- Added `BaseRunner` — headless state machine with SubGraph stack, cycle detection, history rewind

### 0.1.0
- Initial release: data layer (graph, nodes, edges, parameters, actions, conditions, choices)
