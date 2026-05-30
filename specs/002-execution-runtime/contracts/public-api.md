# Public API Contract: GraphCore Execution Runtime

This document defines the stable public surface of the execution runtime layer.
All signatures are frozen per Principle I (Foundation Stability) — changes require
a semver-compliant amendment.

---

## BaseContext

```csharp
namespace Faolline.GraphCore
{
    public class BaseContext
    {
        public void Set<T>(string key, T value);
        public T    Get<T>(string key);
        public bool TryGet<T>(string key, out T value);
        public bool Has(string key);

        public void OnParameterChanged(string key, Action<object> handler);
        public void OffParameterChanged(string key, Action<object> handler);

        public void InitFromGraph(BaseGraph graph);
        public virtual BaseContext DeepClone();
    }
}
```

**Supported generic types for `T`**: `bool`, `int`, `float`, `string`.
Any other type throws `ArgumentException`.

**Contract rules**:
- `Get<T>` throws `KeyNotFoundException` if `key` is absent.
- `TryGet<T>` returns `false` and `default(T)` if `key` is absent; never throws.
- `OnParameterChanged` fires synchronously within `Set<T>`.
- `DeepClone()` returns a fresh instance with values copied, subscriptions cleared.
- `InitFromGraph` silently uses `default(T)` for parse failures (with `[GraphCore]` warning).

---

## INodeExecutor

```csharp
namespace Faolline.GraphCore
{
    public interface INodeExecutor
    {
        string NodeType { get; }
        void Execute(BaseNodeData node, BaseContext context);
        void Undo(BaseNodeData node, BaseContext context) { }  // default no-op
    }
}
```

**Contract rules**:
- `NodeType` MUST be a non-null, non-empty string matching a node's `NodeTypeId` const.
- `Execute` MUST NOT throw for valid node/context inputs.
- `Undo` default is a no-op; implementors MAY override to reverse side-effects.

---

## NodeExecutorRegistry

```csharp
namespace Faolline.GraphCore
{
    public class NodeExecutorRegistry
    {
        public void          Register(INodeExecutor executor);
        public INodeExecutor GetExecutor(string nodeType);
    }
}
```

**Contract rules**:
- `Register` with `executor.NodeType == null` throws `ArgumentNullException`.
- `Register` called twice for the same `NodeType` silently replaces the first.
- `GetExecutor` returns `null` for unregistered types (never throws).

---

## BaseRunner

```csharp
namespace Faolline.GraphCore
{
    public class BaseRunner
    {
        public RunnerState State { get; }

        public event Action<BaseNodeData> OnNodeEntered;
        public event Action<BaseNodeData> OnNodeCompleted;
        public event Action<EndReason>    OnEnded;
        public event Action               OnStuck;

        public void Start(BaseGraph graph, BaseContext context, NodeExecutorRegistry registry);
        public void Proceed();
        public void ChooseById(string id);
        public void GoBack();
        public void GoBackToCheckpoint();
    }
}
```

**Contract rules**:
- `Start` throws `InvalidOperationException` if `graph.EntryNodeId` is null or empty.
- `Start` throws `GraphCycleException` if the graph stack already contains `graph.GraphId`
  (immediate self-cycle).
- `Proceed()` and `ChooseById()` are no-ops when `State == Ended`.
- `GoBack()` and `GoBackToCheckpoint()` are no-ops when history is empty or no checkpoint found.
- All events are `C# Action<T>` — no `UnityEvent`, no `MonoBehaviour` dependency.
- The execution sequence is synchronous; no coroutine or task is created.

---

## RunnerState

```csharp
namespace Faolline.GraphCore
{
    public enum RunnerState
    {
        Idle      = 0,
        NodeReady = 1,
        Paused    = 2,
        Ended     = 3
    }
}
```

---

## GraphCycleException

```csharp
namespace Faolline.GraphCore
{
    public sealed class GraphCycleException : Exception
    {
        public string CyclicGraphId { get; }
        public GraphCycleException(string graphId);
    }
}
```

---

## Semver Assessment

Introducing these types constitutes a **MINOR** bump: `0.1.0 → 0.2.0`.
- `BaseContext` gains a full implementation (existing `abstract` body replaced — non-breaking).
- All other types (`INodeExecutor`, `NodeExecutorRegistry`, `BaseRunner`, `RunnerState`,
  `GraphExecutionState`, `HistoryEntry`, `GraphCycleException`) are new additions.
- No existing public API is removed or modified in a breaking way.

**Stability commitment**: Per Principle I, `INodeExecutor` signatures are frozen.
New methods on `INodeExecutor` MUST have a default implementation.
