# Phase 0 — Research: Flow engine

## R1 — Synchronous token-propagation cascade vs. a step scheduler

**Decision**: One `Fire(nodeId)` resolves the whole reachable sub-flow synchronously: firing a node
immediately delivers tokens to successors, and a target fires the moment its join threshold is met
(recursive cascade).

**Rationale**: Matches the ability-cast shape (cast forks into effects and reconverges at a cooldown in one
instant). Simpler than a tick scheduler and fully deterministic/testable. Timed/persistent active states
(a DoT) are a Flow + P5-Time composition, out of scope.

**Alternatives**: *step-based scheduler (`Step()` advances one wave)* — rejected for the MVP: more API and
state for no present need; the cascade already expresses concurrent firing.

## R2 — Join threshold + one-shot as FlowRunner config vs. graphcore node fields

**Decision**: Per-node **join thresholds** (map) and **one-shot** (set) are supplied to the `FlowRunner`
constructor. graphcore is not changed.

**Rationale**: Keeps graphcore untouched (consistent with P3/P4 living in graphstandard). The default join
threshold is the node's incoming-edge count (AND), so most nodes need no config. Mirrors P4's
config-map approach.

**Alternatives**: *serialized fields on `BaseNodeData`* — rejected: touches graphcore and needs editor work;
deferred to a future authorable Flow-node type.

## R3 — Cycles and the fire-count safety cap

**Decision**: Cycles are allowed (re-pass is intentional). A per-`Fire` fire-count cap (default large, e.g.
10000) bounds runaway propagation; hitting it logs a single `[GraphStandard]` warning and stops the cascade.

**Rationale**: "Back is a re-pass, not undo" means revisiting/re-firing is legitimate, so the engine must
not forbid cycles. But unbounded cycles would hang; a cap makes it safe. Authors terminate cleanly with
one-shot marks or edge conditions.

**Alternatives**: *forbid cycles (detect + throw)* — rejected: contradicts the re-pass intent. *no cap* —
rejected: risks an infinite loop / editor hang.

## R4 — Firing runs `OnEnterActions`; edge conditions gate

**Decision**: Firing a node runs its graphcore `OnEnterActions` against the shared context and emits
`OnNodeFired`. Propagation along an outgoing edge happens only if the edge's `Condition` passes (or is null).

**Rationale**: Reuses graphcore's existing action and edge-condition model — a flow node can mutate context
(add to a collection, set a parameter) exactly as in the Linear runner, and gating is uniform across engines.
No new node behaviour invented.

## R5 — Arrived tokens keyed by edge id, cleared on fire

**Decision**: For each node, track the set of **incoming edge ids** that have delivered a token. A node fires
when that set's size reaches its join threshold; on firing, the set is **cleared** (tokens consumed).

**Rationale**: Keying by edge id makes the AND-join exact (each distinct incoming edge contributes once) and
de-duplicates a repeated delivery from the same edge within one cascade. Clearing on fire enables a clean
re-pass: a later propagation re-accumulates from fresh tokens.
