# Quickstart — P3 Reactive engine

How a **host** drives a progression DAG with the `ReactiveEvaluator`, in the new `com.faolline.graphstandard`
lib (depends on graphcore 0.5.0). Headless / EditMode-testable.

## 1. Author a prerequisite DAG

Reuse the graphcore substrate. An edge `A→C` means **"C requires A"** (dependency, not flow):

```
A ─┐
   ├─► C          (C requires A AND B)
B ─┘
```

```csharp
graph.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "A", ToNodeId = "C" });
graph.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "B", ToNodeId = "C" });
```

## 2. Initialize the evaluator

```csharp
var ctx = new BaseContext();
var eval = new ReactiveEvaluator(graph, ctx, completedSetKey: "completed");

eval.OnNodeAvailable += id => Debug.Log($"available: {id}");
eval.OnNodeCompleted += id => Debug.Log($"completed: {id}");

// Construct, subscribe, THEN Start() to receive the initial emission (here: available A, B).
eval.Start();

eval.GetState("A");   // Available  (no prerequisites — already derived at construction)
eval.GetState("C");   // Locked     (A, B not yet completed)
```

## 3. Mark complete → cascade unlock

```csharp
eval.MarkCompleted("A");   // C still Locked (B missing)
eval.GetState("C");        // Locked
eval.MarkCompleted("B");   // → OnNodeCompleted("B"), then OnNodeAvailable("C")
eval.GetState("C");        // Available
eval.MarkCompleted("A");   // already complete → no-op, no events
```

Query the live sets any time:

```csharp
eval.CompletedNodeIds;   // { "A", "B" }
eval.AvailableNodeIds;   // { "C" }  (A, B are Completed, not Available)
```

## 4. Durable & reversible (re-pass, not undo)

Completion lives in the graphcore P2 collection `"completed"`, so it persists (save) and history-restores.
After restoring the context to an earlier state, re-evaluate:

```csharp
// ... host restores ctx so the "completed" set is back to { "A" } (e.g. via graphcore history) ...
eval.Reevaluate();
eval.GetState("C");   // Locked again  — re-pass, no node "undo" side-effects ran
eval.GetState("B");   // Available
```

`Reevaluate()` derives state purely from the current completed-set, so shrinking the set yields the smaller
satisfied set deterministically.

## 5. Key rules

- **Edges are prerequisites** (target requires source); the engine has **no cursor** — many nodes can be
  Available at once.
- **Available = all prerequisites Completed** (AND). Threshold / OR is the P4 Join (later).
- **Completion is host-driven** (`MarkCompleted`); the engine trusts the host about legality.
- **"Back" is a re-pass**: state is derived from the completed-set, never an undo of side-effects.
- **graphcore is untouched** — this is a new lib on top of it.

## 6. Verify

`com.faolline.graphstandard`'s EditMode tests cover state derivation, the unlock cascade, events, and a
game-like multi-tier progression DAG with restore/re-pass — all headless, editor closed.
