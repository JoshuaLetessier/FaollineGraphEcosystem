# Phase 1 — Public API Contract: P3 Reactive engine

New public surface in `com.faolline.graphstandard` (0.1.0). graphcore is unchanged.

## `Faolline.GraphStandard.ReactiveNodeState`

```csharp
public enum ReactiveNodeState { Locked = 0, Available = 1, Completed = 2 }
```

## `Faolline.GraphStandard.ReactiveEvaluator`

```csharp
public ReactiveEvaluator(BaseGraph graph, BaseContext context, string completedSetKey);
public event Action<string> OnNodeAvailable;
public event Action<string> OnNodeCompleted;
public void MarkCompleted(string nodeId);
public void Reevaluate();
public ReactiveNodeState GetState(string nodeId);
public IReadOnlyCollection<string> AvailableNodeIds { get; }
public IReadOnlyCollection<string> CompletedNodeIds { get; }
```

## Invariants

- INV-1 (derivation): `GetState(n)` = Completed if `n` ∈ completed-set; else Available if `n` has no
  prerequisites or all are Completed; else Locked. Prerequisites of `n` = sources of edges with `ToNodeId == n`.
- INV-2 (AND): a multi-prerequisite node is Available only when ALL prerequisites are Completed.
- INV-3 (mark): `MarkCompleted(n)` adds `n` to the completed-set and re-evaluates; an already-completed `n`
  is a no-op (no duplicate, no events).
- INV-4 (events): `OnNodeAvailable`/`OnNodeCompleted` fire exactly when a node enters that state; no event
  on an unchanged state; initialization emits for initially Available/Completed nodes.
- INV-5 (queries): `AvailableNodeIds`/`CompletedNodeIds` reflect the current derived states; `GetState`
  returns Locked for an unknown id.
- INV-6 (reversible): after the host restores the context to a smaller completed-set and calls
  `Reevaluate`, states recompute to the smaller satisfied set; no node undo side-effects run.
- INV-7 (durable): completion lives only in the graphcore P2 completed-set collection (persists + history).
- INV-8 (no traversal): no current node; multiple nodes may be Available simultaneously.
- INV-9 (robust): an id in the set but not in the graph is ignored; cyclic prerequisites never loop and
  never auto-become-Available.
- INV-10 (graphcore untouched): no graphcore Runtime change; graphstandard depends on graphcore 0.5.0.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1.1 no-prereq Available | INV-1 |
| US1.2 partial prereqs Locked | INV-1/INV-2 |
| US1.3 all prereqs ⇒ Available | INV-2 |
| US1.4 in set ⇒ Completed | INV-1 |
| US2.1 cascade unlock | INV-2/INV-3 |
| US2.2 idempotent re-mark | INV-3 |
| US2.3 set contains marked id | INV-3/INV-7 |
| US3.1 init emits Available for A,B not C | INV-4 |
| US3.2 complete B ⇒ Completed(B)+Available(C) | INV-4 |
| US3.3 re-mark emits nothing | INV-3/INV-4 |
| US4.1 restore shrinks ⇒ C Locked | INV-6 |
| US4.2 idempotent derivation | INV-1/INV-6 |
| SC-005 graphcore untouched | INV-10 |
