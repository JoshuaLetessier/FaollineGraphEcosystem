# Phase 1 — Public API Contract: Flow engine

New public surface in `com.faolline.graphstandard` 0.3.0. graphcore unchanged.

## `Faolline.GraphStandard.FlowRunner`

```csharp
public FlowRunner(
    BaseGraph graph,
    BaseContext context,
    IReadOnlyCollection<string> oneShotNodeIds = null,
    IReadOnlyDictionary<string, int> joinThresholds = null,
    int maxFiresPerPropagation = 10000);

public event Action<string> OnNodeFired;
public void Fire(string nodeId);
public void Reset();
public bool HasFired(string nodeId);
public IReadOnlyCollection<string> FiredNodeIds { get; }
```

## Invariants

- INV-1 (fork): `Fire` propagates along every condition-passing outgoing edge of each fired node.
- INV-2 (join): a node fires when the count of distinct incoming edges that delivered a token reaches its
  threshold (default = incoming-edge count = AND; `joinThresholds` overrides per node).
- INV-3 (work + event): firing runs the node's `OnEnterActions` against the context, then emits `OnNodeFired`.
- INV-4 (gate): a false/edge `Condition` delivers no token.
- INV-5 (re-pass): a non-one-shot node may fire again; arrived tokens clear on fire.
- INV-6 (one-shot): an id in `oneShotNodeIds` fires at most once until `Reset`.
- INV-7 (cycle cap): propagation halts at `maxFiresPerPropagation` with one `[GraphStandard]` warning.
- INV-8 (direct fire): `Fire(id)` always fires `id`; token delivery respects the threshold.
- INV-9 (robust): unknown id ignored; threshold > incoming ⇒ never token-fires; token to an already-fired
  one-shot ignored.
- INV-10 (queries): `HasFired`/`FiredNodeIds` reflect fires since the last `Reset`.
- INV-11 (graphcore untouched): only graphcore's public surface is used; graphstandard 0.2.0 → 0.3.0.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1.1 fork all | INV-1 |
| US1.2 false edge no fire | INV-4 |
| US1.3 chain cascades | INV-1 |
| US2.1 AND-join | INV-2 |
| US2.2 OR-join (k=1) | INV-2 |
| US2.3 join fires once | INV-2/INV-5 |
| US3.1 re-fire | INV-5 |
| US3.2 one-shot once | INV-6 |
| US3.3 Reset re-arms | INV-6/INV-10 |
| US3.4 cycle bounded | INV-7 |
| US4.1 action mutates context | INV-3 |
| US4.2 conditional edge | INV-4 |
| US4.3 ability cast resolves | INV-1/INV-2/INV-3 |
| SC-007 graphcore untouched, suite green | INV-11 |
