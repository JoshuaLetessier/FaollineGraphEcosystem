# Feature Specification: Flow engine (multi-active fork/join, re-pass, one-shot)

**Feature Branch**: `019-flow-engine`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "The third execution engine — Flow — a cursor-less, MULTI-ACTIVE token-propagation engine over the graphcore substrate. Firing a node forks to ALL valid outgoing edges (activate-all, not select-one); a node with multiple incoming edges fires on a JOIN (a k-of-N rendezvous, default = all incoming = AND). Firing runs the node's actions and emits an event. Re-pass is allowed (a node may fire again; cycles permitted, bounded by a safety cap); a per-node ONE-SHOT mark fires at most once until Reset. Models ability/spell execution (cast → fork into damage/debuff/vfx → join → cooldown) and multidirectional flows. Lives in com.faolline.graphstandard (0.2.0 → 0.3.0); graphcore untouched."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Host integrator** — fires an entry node (a "cast", an event) and reacts to nodes firing.
- **Graph author** — designs a fork/join flow (an ability, a multidirectional sequence).
- **(transitive) abilities / combat lib developer** — builds spell/skill execution on this engine.

> **Substrate reinterpretation**: like Linear and Reactive, Flow reads the same graphcore data, but an edge
> means a **flow connection** (fire the source ⇒ propagate to the target). It has no single cursor — many
> nodes fire within one propagation.

### User Story 1 - Fork: firing a node activates all valid successors (Priority: P1) 🎯 MVP

Firing a node runs its work and **forks**: it propagates to **every** outgoing edge whose condition passes
(not just the first). This is the multi-active core — one fire can light up many downstream nodes.

**Why this priority**: Fork is the defining difference from the Linear runner (which selects one edge). It
is the basis of concurrent effects (an ability hitting several systems at once).

**Independent Test**: cast → {damage, debuff, vfx}. Fire("cast") → cast, damage, debuff, and vfx all fire.

**Acceptance Scenarios**:

1. **Given** a node with three outgoing edges (no conditions), **When** it is fired, **Then** all three target nodes fire.
2. **Given** an outgoing edge whose condition is false, **When** the source fires, **Then** that edge's target does NOT fire (others still do).
3. **Given** a chain A→B→C, **When** A is fired, **Then** A, B, and C all fire (propagation cascades).

### User Story 2 - Join: a node fires on a k-of-N rendezvous (Priority: P1)

A node with multiple incoming edges **joins**: it fires only once enough of its incoming branches have
delivered a token. The default join threshold is **all** incoming edges (AND-rendezvous); it is
configurable per node (k-of-N), reusing the threshold idea from the Reactive join.

**Why this priority**: Join is the other half of fork — it lets concurrent branches reconverge (e.g. apply
a cooldown only after damage AND debuff AND vfx have run). Fork + Join = the MVP.

**Independent Test**: damage, debuff, vfx all → cooldown (3 incoming, default AND). Firing the fork once ⇒
cooldown fires exactly once, after all three. With a join threshold of 1, cooldown fires on the first arrival.

**Acceptance Scenarios**:

1. **Given** a node with two incoming edges and the default (AND) threshold, **When** only one predecessor fires, **Then** it does NOT fire; **When** the second fires, **Then** it fires once.
2. **Given** a join node configured with threshold 1, **When** any one predecessor fires, **Then** it fires (OR-join).
3. **Given** a fork that reconverges at a join, **When** the fork is fired once, **Then** the join fires exactly once.

### User Story 3 - Re-pass and one-shot (Priority: P2)

Re-firing is allowed: a node may fire again on a later propagation (cycles are permitted, bounded by a
safety cap to prevent runaway). A per-node **one-shot** mark makes a node fire at most once until the engine
is **reset**; `Reset` clears all fired/one-shot/token state for a fresh pass (a re-cast).

**Why this priority**: "Back is a re-pass, not undo" (the research) and one-shot effects (fire a VFX once)
are essential to abilities; but US1+US2 already deliver a working fork/join engine.

**Independent Test**: a one-shot node fires once across two fires of its trigger; a non-one-shot node fires
each time; after Reset, a one-shot node can fire again.

**Acceptance Scenarios**:

1. **Given** a non-one-shot node, **When** its trigger is fired twice, **Then** it fires twice.
2. **Given** a one-shot node, **When** its trigger is fired twice (no reset), **Then** it fires once.
3. **Given** a one-shot node that has fired, **When** `Reset` is called and it is triggered again, **Then** it fires.
4. **Given** a cyclic graph, **When** fired, **Then** propagation is bounded (a `[GraphStandard]` warning is logged at the safety cap) rather than looping forever.

### User Story 4 - Firing does work; conditions gate; ability-cast scenario (Priority: P2)

Firing a node runs its node actions against the shared context (so a flow node can mutate state — add to a
collection, set a parameter), and outgoing edge conditions gate propagation. A game-like ability cast
(cast → fork into effects → join → cooldown) runs headless.

**Why this priority**: Makes the engine actually *do* something and proves the end-to-end ability shape.

**Independent Test**: a cast flow where one effect node adds an id to a collection; after firing, the
collection contains it; a conditional edge only propagates when the condition holds.

**Acceptance Scenarios**:

1. **Given** a node with an enter-action that writes the context, **When** it fires, **Then** the context shows the write.
2. **Given** an edge gated by a condition over the context, **When** the source fires, **Then** the target fires only if the condition passes.
3. **Given** a cast→{damage,debuff,vfx}→cooldown flow, **When** cast is fired, **Then** every node fires once and cooldown fires last.

### Edge Cases

- **A node with no outgoing edges** → fires and the cascade ends there (a sink).
- **A condition-false edge** → no token delivered along it (its share never counts toward the target's join).
- **Cycle** → propagation is bounded by a fire-count safety cap; a `[GraphStandard]` warning is logged; one-shot marks / edge conditions are the author's tools to terminate cleanly.
- **Firing an unknown node id** → ignored (no crash).
- **Join threshold > incoming count** → the node never fires from tokens (only a direct `Fire` triggers it).
- **A token arriving at an already-fired one-shot node** → ignored.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Firing a node MUST propagate to EVERY outgoing edge whose condition passes (fork / activate-all), not a single selected edge.
- **FR-002**: A node MUST fire only when the number of its incoming edges that have delivered a token reaches its **join threshold**; the default threshold is the node's incoming-edge count (AND-rendezvous), configurable per node (k-of-N).
- **FR-003**: Firing a node MUST run that node's enter-actions against the shared context and emit a "node fired" event.
- **FR-004**: Outgoing edge conditions MUST gate propagation (a false condition delivers no token).
- **FR-005**: Re-firing MUST be allowed — a non-one-shot node may fire again on a later propagation; cycles MUST be permitted but bounded by a fire-count safety cap that logs a `[GraphStandard]` warning instead of looping forever.
- **FR-006**: A per-node **one-shot** mark MUST cause a node to fire at most once until `Reset`; `Reset` MUST clear all fired/one-shot/token state.
- **FR-007**: The host MUST be able to fire an entry node directly (the external trigger), bypassing the join threshold for that direct fire.
- **FR-008**: The engine MUST expose the set/identification of nodes fired and a way to query whether a node has fired (since the last reset).
- **FR-009**: Unknown node ids and join thresholds greater than the incoming count MUST be handled gracefully (no crash; such a node simply never auto-fires).
- **FR-010**: graphcore MUST be unchanged; the engine MUST live in `com.faolline.graphstandard` (0.2.0 → 0.3.0, semver MINOR, additive). It MUST be headless (no MonoBehaviour/UnityEvent; C# events) and verifiable in EditMode.
- **FR-011**: The engine MUST encode only universal flow semantics (fork, join, re-pass, one-shot); zero domain vocabulary.

### Key Entities

- **FlowRunner**: the multi-active token-propagation engine.
- **Token**: a propagation mark delivered along an edge toward a target's join.
- **Join threshold**: per node, how many incoming branches must arrive before it fires (default = incoming count = AND).
- **One-shot mark**: a per-node flag making it fire at most once until reset.
- **Fired set**: the node ids that have fired since the last reset.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Firing a node with three unconditional successors fires all three (fork).
- **SC-002**: A 3-incoming join with the default threshold fires exactly once after all three predecessors fire; with threshold 1 it fires on the first.
- **SC-003**: A one-shot node fires once across repeated triggers; a non-one-shot node fires each time; `Reset` re-arms one-shots.
- **SC-004**: A cyclic graph terminates with a `[GraphStandard]` warning at the safety cap (no infinite loop / hang).
- **SC-005**: A node's enter-action mutates the shared context on fire; a conditional edge gates propagation.
- **SC-006**: A game-like ability cast (cast → fork → effects → join → cooldown) runs headless: all nodes fire once, cooldown last.
- **SC-007**: graphcore is untouched; the entire existing 621-test suite stays green; graphstandard adds its own green tests; graphstandard 0.2.0 → 0.3.0.

## Assumptions

- **Synchronous cascade**: one `Fire` resolves the whole reachable sub-flow in a single propagation
  (instant fork/join, as for an ability cast). Persistent/timed active states (a DoT ticking over seconds)
  are a Flow + P5-Time composition, deferred.
- **Edges are flow connections**; **join threshold defaults to incoming count** (AND), per-node configurable
  (reusing the P4 k-of-N idea). **One-shot** and **join thresholds** are FlowRunner configuration (node-id
  sets/maps), NOT graphcore node fields — keeping graphcore untouched.
- **Firing runs the node's enter-actions** (graphcore's existing action model) and emits an event; edge
  conditions gate propagation.
- **Cycles are allowed** but bounded by a fire-count safety cap (warned), since re-pass is intentional;
  authors terminate with one-shot marks or conditions.
- **Governance**: EditMode TDD; `[GraphStandard]` prefix; one class per file; XML docs; graphstandard 0.2.0 → 0.3.0.

## Out of Scope *(deferred)*

- Timed / persistent active states (Flow + Time composition); per-frame "active duration" of a node.
- The resolution-ordering / priority policy (axis A) beyond the deterministic cascade order.
- A serialized one-shot/threshold node field with an authoring inspector (config-supplied in this MVP).
- Promoting standard nodes; any visual/editor authoring of flow graphs; the abilities/combat domain lib itself.
