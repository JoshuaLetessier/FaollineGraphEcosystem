# Data Model: GraphCore Execution Runtime

## Overview

All types live in the `Faolline.GraphCore` namespace inside the `com.faolline.graphcore.Runtime`
assembly. No new assembly is added — all execution types join the existing Runtime assembly.

---

## BaseContext *(replaces the empty placeholder from 001-data-layer)*

**Kind**: Concrete class (was `abstract` — see research.md Decision 2)
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Graph/BaseContext.cs`

**Internal state**:

| Field | Type | Notes |
|-------|------|-------|
| `_params` | `Dictionary<string, object>` | Stores all typed parameter values. Keys are parameter key strings. |
| `_subs` | `Dictionary<string, List<Action<object>>>` | Per-key subscriber lists for change notifications. |

**Public API**:

```
void   Set<T>(string key, T value)                       // T ∈ {bool, int, float, string}
T      Get<T>(string key)                                // throws KeyNotFoundException if absent
bool   TryGet<T>(string key, out T value)                // false + default(T) if absent
bool   Has(string key)

void   OnParameterChanged(string key, Action<object> handler)
void   OffParameterChanged(string key, Action<object> handler)

void          InitFromGraph(BaseGraph graph)             // populates from graph.Parameters
virtual BaseContext DeepClone()                          // copies _params; does NOT copy _subs
```

**Invariants**:
- `Set<T>` throws `ArgumentException` if `T` is not in `{bool, int, float, string}`.
- `InitFromGraph` converts `ParameterData.DefaultValue` (string) using `bool.Parse`,
  `int.Parse`, `float.Parse` (invariant culture), or identity for string. Parse failures
  use `default(T)` and log a `[GraphCore]` warning.
- `DeepClone` creates a `new BaseContext()` and shallow-copies `_params`. `_subs` is NOT copied.
- `Set<T>` fires all handlers in `_subs[key]` after updating the value, passing the new value
  boxed as `object`.

**Extension**: Downstream libs subclass `BaseContext` and override `DeepClone()` to copy their
additional fields alongside the base parameter dict.

---

## INodeExecutor

**Kind**: Interface
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/INodeExecutor.cs`

```csharp
public interface INodeExecutor
{
    string NodeType { get; }
    void Execute(BaseNodeData node, BaseContext context);
    void Undo(BaseNodeData node, BaseContext context) { }   // C# 8 default no-op
}
```

**Invariants**:
- `NodeType` MUST match a `NodeTypeId` const from a node class (e.g., `"graphcore/statement"`).
- `Execute` is called by `BaseRunner` during the node execution sequence.
- `Undo` is called by `BaseRunner.GoBack()` if the implementor overrides it; the default
  no-op is used by executors that do not need undo support.

---

## NodeExecutorRegistry

**Kind**: Concrete class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/NodeExecutorRegistry.cs`

**Internal state**:

| Field | Type | Notes |
|-------|------|-------|
| `_executors` | `Dictionary<string, INodeExecutor>` | Maps NodeType → executor. |

**Public API**:

```
void           Register(INodeExecutor executor)          // replaces existing silently; null NodeType → ArgumentNullException
INodeExecutor  GetExecutor(string nodeType)              // returns null for unknown types
```

---

## RunnerState

**Kind**: Enum
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/RunnerState.cs`

```
Idle      = 0   // Before Start() is called
NodeReady = 1   // Current node is ready; waiting for Proceed()/ChooseById()
Paused    = 2   // SubGraph pushed; execution suspended in sub-graph entry
Ended     = 3   // EndNode reached; execution complete
```

---

## GraphExecutionState

**Kind**: Class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/GraphExecutionState.cs`

| Field | Type | Notes |
|-------|------|-------|
| `Graph` | `BaseGraph` | The graph being executed at this stack level. Asset reference (shared, read-only). |
| `CurrentNodeId` | `string` | Id of the node currently being processed. |
| `AvailableEdges` | `List<BaseEdgeData>` | Outgoing edges from the current node (computed on entry). |

**Shallow clone**: `new GraphExecutionState { Graph = this.Graph, CurrentNodeId = this.CurrentNodeId, AvailableEdges = new List<BaseEdgeData>(this.AvailableEdges) }`.
The `BaseGraph` asset reference is shared (assets are immutable during runtime).

---

## HistoryEntry

**Kind**: Class (immutable after construction)
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/HistoryEntry.cs`

| Field | Type | Notes |
|-------|------|-------|
| `NodeId` | `string` | The node id at the time of snapshot. |
| `GraphStackSnapshot` | `Stack<GraphExecutionState>` | Clone of the full graph stack (shallow clone per frame above). |
| `ContextSnapshot` | `BaseContext` | Deep clone of the context at this point (`DeepClone()`). |

**Invariants**:
- Captured AFTER `OnExitActions` and BEFORE advancing to the next node.
- `GraphStackSnapshot` is a snapshot of the stack at that instant — future pushes/pops
  do not affect it.

---

## BaseRunner

**Kind**: Concrete class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/BaseRunner.cs`

**Internal state**:

| Field | Type | Notes |
|-------|------|-------|
| `_state` | `RunnerState` | Current machine state. |
| `_graphStack` | `Stack<GraphExecutionState>` | Active SubGraph stack. Top = currently executing frame. |
| `_history` | `List<HistoryEntry>` | Bounded history. Index 0 = oldest. |
| `_context` | `BaseContext` | The live execution context. |
| `_registry` | `NodeExecutorRegistry` | Executor lookup. |

**Public API**:

```
RunnerState State { get; }

// Events (C# Action<T> only)
event Action<BaseNodeData>  OnNodeEntered;
event Action<BaseNodeData>  OnNodeCompleted;   // raised after Execute(); caller calls Proceed()
event Action<EndReason>     OnEnded;
event Action                OnStuck;           // raised when no valid outgoing edge exists

// Lifecycle
void Start(BaseGraph graph, BaseContext context, NodeExecutorRegistry registry)
void Proceed()
void ChooseById(string id)
void GoBack()
void GoBackToCheckpoint()
```

**Execution sequence per node** (invoked internally on each transition):

1. Evaluate `node.EntryConditions` — all must return `true`; if any fails, raise `OnStuck` and stop.
2. Execute `node.OnEnterActions` in order.
3. Call `registry.GetExecutor(node.NodeType)?.Execute(node, context)` (no-op if null).
4. Raise `OnNodeCompleted(node)` — runner pauses here until `Proceed()` or `ChooseById()`.
5. Execute `node.OnExitActions` in order.
6. Evaluate outgoing edges to find next node (or detect stuck/ended).
7. Append `HistoryEntry` snapshot; trim if over `HistoryDepth`.
8. Advance: push SubGraph stack frame (if SubGraphNode) or set next `CurrentNodeId`.

**SubGraph entry** (`SubGraphNodeData` reached):
- Check cycle: if `targetGraph.GraphId` is in `_graphStack` → throw `GraphCycleException`.
- Determine context: `InheritParentContext = true` → pass `_context`; `false` → `new BaseContext()` + `InitFromGraph(targetGraph)`.
- Push new `GraphExecutionState` for `targetGraph` onto `_graphStack`.
- Set `_state = Paused` momentarily; resume with `NodeReady` at sub-graph entry node.

**SubGraph exit** (`EndNodeData` reached while stack depth > 1):
- Pop `_graphStack`.
- Resume at parent's next node (`Proceed()` on the popped-to `GraphExecutionState`).

**GoBack()** behavior:
1. If `_history` is empty: no-op.
2. Take last `HistoryEntry`, remove it from `_history`.
3. Call `registry.GetExecutor(currentNode.NodeType)?.Undo(currentNode, _context)` on the current node before restoring.
4. Restore `_graphStack` from `entry.GraphStackSnapshot`.
5. Restore `_context` parameter values from `entry.ContextSnapshot`.
6. Set `_state = NodeReady`.

**GoBackToCheckpoint()** behavior:
1. Search `_history` from newest to oldest for `entry.NodeId` where that node has `IsCheckpoint = true`.
2. If none found: no-op.
3. Truncate `_history` to that entry (inclusive), then apply `GoBack()` on it.

---

## GraphCycleException

**Kind**: Exception class (extends `System.Exception`)
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Execution/GraphCycleException.cs`

```csharp
public sealed class GraphCycleException : Exception
{
    public string CyclicGraphId { get; }

    public GraphCycleException(string graphId)
        : base($"[GraphCore] Cycle detected: graph '{graphId}' is already in the execution stack.")
    {
        CyclicGraphId = graphId;
    }
}
```

---

## Type Relationships

```
BaseRunner
  ├── _graphStack: Stack<GraphExecutionState>
  │     └── GraphExecutionState
  │           ├── Graph: BaseGraph              (001 data layer)
  │           ├── CurrentNodeId: string
  │           └── AvailableEdges: List<BaseEdgeData>
  ├── _history: List<HistoryEntry>
  │     └── HistoryEntry
  │           ├── NodeId: string
  │           ├── GraphStackSnapshot: Stack<GraphExecutionState> (cloned)
  │           └── ContextSnapshot: BaseContext  (DeepClone)
  ├── _context: BaseContext
  │     ├── _params: Dictionary<string, object>
  │     └── _subs: Dictionary<string, List<Action<object>>>
  └── _registry: NodeExecutorRegistry
        └── _executors: Dictionary<string, INodeExecutor>
              └── INodeExecutor
                    ├── NodeType: string
                    ├── Execute(BaseNodeData, BaseContext)
                    └── Undo(BaseNodeData, BaseContext) [default no-op]

BaseRunner events: Action<BaseNodeData> OnNodeEntered, OnNodeCompleted
                   Action<EndReason>    OnEnded
                   Action               OnStuck

GraphCycleException (thrown on cycle — carries CyclicGraphId)
```
