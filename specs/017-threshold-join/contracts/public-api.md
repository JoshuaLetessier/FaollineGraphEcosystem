# Phase 1 — Public API Contract: P4 Generic threshold Join

Additive public surface in `com.faolline.graphstandard` 0.2.0. graphcore unchanged.

## `Faolline.GraphStandard.ReactiveEvaluator` (additive)

```csharp
// New: optional required-counts configuration (node id -> k). null/absent ⇒ default AND (k = N) per node.
public ReactiveEvaluator(
    BaseGraph graph,
    BaseContext context,
    string completedSetKey,
    IReadOnlyDictionary<string, int> requiredCounts = null);
```

All other members (`OnNodeAvailable`, `OnNodeCompleted`, `MarkCompleted`, `Reevaluate`, `Start`,
`GetState`, `AvailableNodeIds`, `CompletedNodeIds`) are **unchanged**.

## Invariants

- INV-1 (default AND): no configured count for a node ⇒ Available iff ALL prerequisites Completed (P3).
- INV-2 (threshold): a node with required count k is Available iff at least k of its prerequisites are
  Completed and the node is not itself Completed.
- INV-3 (spectrum): k=1 ⇒ OR; k=N ⇒ AND; 1<k<N ⇒ N-of-M.
- INV-4 (boundaries): k≤0 ⇒ ungated (Available unless Completed); k>N ⇒ never auto-available from
  prerequisites (Locked until host-completed); neither throws.
- INV-5 (lifecycle): the threshold is honored by state derivation, the unlock cascade, the
  availability/completion events, `Start` emission, and `Reevaluate` (reversible).
- INV-6 (robust): a `requiredCounts` entry for an unknown id is ignored; duplicate prerequisites count once.
- INV-7 (source-compatible): the 3-arg constructor still compiles and behaves as P3 (default AND).
- INV-8 (graphcore untouched): no graphcore change; graphstandard 0.1.0 → 0.2.0.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1.1 default needs all | INV-1 |
| US1.2 k=2 any two | INV-2 |
| US1.3 ≥ threshold available | INV-2 |
| US1.4 no-prereq available | INV-1/INV-4 |
| US2.1 k=1 OR | INV-3 |
| US2.2 k≤0 ungated | INV-4 |
| US2.3 k>N never auto | INV-4 |
| US2.4 k=N ≡ AND | INV-1/INV-3 |
| US3.1 event at threshold | INV-5 |
| US3.2 re-lock on step-back | INV-5 |
| US3.3 default ≡ P3 | INV-1/INV-7 |
| SC-002 suite green | INV-1/INV-7 |
| SC-005 graphcore untouched | INV-8 |
