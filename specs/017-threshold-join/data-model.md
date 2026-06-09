# Phase 1 — Data Model: P4 Generic threshold Join

Additive change inside `com.faolline.graphstandard` `ReactiveEvaluator` (0.1.0 → 0.2.0). graphcore and
`ReactiveNodeState` unchanged.

## `ReactiveEvaluator` — additions

New constructor (the existing 3-arg constructor remains valid as the default-AND path):
- `ReactiveEvaluator(BaseGraph graph, BaseContext context, string completedSetKey, IReadOnlyDictionary<string,int> requiredCounts = null)`
  — `requiredCounts` maps a node id to its required number of Completed prerequisites k. `null` (or absent)
  means every node uses the default (k = its prerequisite count N = AND).

New private field:
- `Dictionary<string, int> _requiredCounts` — a copy of the supplied map (empty when none).

### Refined derivation rule (`DeriveState`)

```
if completed-set contains nodeId                      -> Completed
else:
    prereqs   = prerequisites(nodeId)                 // de-duplicated incoming-edge sources (P3)
    N         = prereqs.Count
    completed = count of prereqs that are in the completed-set
    k         = _requiredCounts.TryGetValue(nodeId)? configured value : N      // default N = AND
    (completed >= k) ? Available : Locked
```

**Invariants**
- T1 — **Default AND**: with no configured count, k = N, so a node is Available only when all prerequisites
  are Completed — identical to P3 (FR-002, SC-002).
- T2 — **Spectrum**: k=1 ⇒ OR (any one), k=N ⇒ AND, 1<k<N ⇒ N-of-M (FR-004).
- T3 — **Boundaries**: k ≤ 0 ⇒ `completed >= k` always true ⇒ Available (unless Completed); k > N ⇒
  `completed` maxes at N < k ⇒ never Available from prerequisites (FR-005), no error.
- T4 — **No-prereq node**: N = 0; default k = 0 ⇒ Available; configured k ≥ 1 ⇒ never auto-available.
- T5 — **Robust config**: a `requiredCounts` entry for an unknown node id is simply never consulted;
  prerequisites are a set so duplicate edges count once (FR-007).
- T6 — **Lifecycle-wide**: because cascade/events/Start/Reevaluate all call `DeriveState`, the threshold is
  honored across the whole engine with no other change (FR-006).
- T7 — **Source-compatible**: the new ctor parameter is optional; P3 callers (3-arg) bind `null` and get
  default AND.

## Unchanged

- `ReactiveNodeState`, the events, `MarkCompleted`, `Start`, `Reevaluate`, the queries, the prerequisite
  map construction — all unchanged except that `DeriveState` now consults `_requiredCounts`.
- graphcore: no change.
