# Phase 0 — Research: re-lock event + doc clarity

## R1 — Emit from the existing `EmitFor`

**Decision**: Raise `OnNodeLocked` by adding one branch to the private `EmitFor(nodeId, state)`:
`else if (state == ReactiveNodeState.Locked) OnNodeLocked?.Invoke(nodeId);`.

**Rationale**: `EmitFor` is the single choke point already used by both `Start()` (initial emission) and
`Reevaluate()` (transition emission). Routing the new event through it guarantees the re-lock event has exactly
the same firing semantics as `OnNodeAvailable`/`OnNodeCompleted` — fires on transition and on initial emission,
never on an unchanged node (the caller only invokes `EmitFor` on `!known || prev != state`). No new state, no
change to derivation.

**Alternatives**: *track and diff Locked transitions separately* — rejected: duplicates logic `Reevaluate`
already does. *fire only on transition, not initial emission* — rejected: breaks symmetry with the other two
events and needs a special case.

## R2 — Initial-emission symmetry is intended

**Decision**: `Start()` raising `OnNodeLocked` for every initially-Locked node is desired behavior.

**Rationale**: It mirrors `Start()` already raising Available/Completed for initially-Available/Completed nodes
— a host that paints from these events gets a complete initial picture. The event is brand new, so no existing
subscriber can be surprised.

## R3 — Documentation leads with `MarkCompleted`

**Decision**: The README reactive-hosting section leads with owning the evaluator and calling `MarkCompleted`
(which re-derives internally). The `AddToCollectionAction` + `OnCollectionChanged → Reevaluate` bridge is shown
as the **alternative** for when a Linear-flow action writes the set, with an explicit "not both — double
evaluation" caveat. The new `OnNodeLocked` event is documented alongside the other two.

**Rationale**: Round-4 feedback: a real consumer routed completion via `MarkCompleted` and needed a re-read to
see the bridge was an alternative, not a requirement. Leading with the simplest path and stating exclusivity
removes that pause.
