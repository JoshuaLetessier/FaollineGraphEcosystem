# Feature Specification: P3 — Reactive engine (ReactiveEvaluator)

**Feature Branch**: `016-reactive-engine`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "P3 — Reactive engine: a cursor-less evaluator over the graphcore substrate that tracks per-node state Locked/Available/Completed and recomputes the satisfied set on change. A node is Available when all its prerequisites (incoming-edge sources) are Completed (AND); no-prerequisite nodes are Available from the start; completion is host-driven and recorded in a P2 string-set collection. Lives in a NEW buffer lib com.faolline.graphstandard (0.1.0) depending on graphcore 0.5.0. graphcore is untouched. MVP = AND prerequisites + host-driven MarkCompleted; threshold/OR (P4), Flow engine, time, and the questsystem lib are out of scope."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Host integrator** — drives progression: marks nodes complete and reacts to availability/completion events (unlock UI, spawn content).
- **Graph author** — designs a **prerequisite DAG** (an edge A→C means "C requires A").
- **(transitive) questsystem / skill-tree lib developer** — builds a domain progression lib on this engine.

> **Substrate reinterpretation**: the Reactive engine reads the SAME graphcore data as the Linear runner,
> but an edge means a **dependency** (target requires source), not flow order. The engine has no cursor.

### User Story 1 - Derive node states from the DAG and the completed-set (Priority: P1) 🎯 MVP

Given a graph and a "completed-set" (the ids of completed nodes), the evaluator reports each node's
**state**: **Completed** if its id is in the completed-set; otherwise **Available** if it has no
prerequisites or all its prerequisite nodes are Completed; otherwise **Locked**. There is no traversal —
state is derived, not walked.

**Why this priority**: This derivation IS the reactive paradigm; everything else (cascades, events,
durability) is built on it. It replaces the imperative "which puzzles are available" spaghetti.

**Independent Test**: DAG A,B→C (C requires A and B). With an empty completed-set: A and B are Available,
C is Locked. With {A}: A Completed, B Available, C still Locked. With {A,B}: C Available.

**Acceptance Scenarios**:

1. **Given** a node with no prerequisites and an empty completed-set, **When** the state is queried, **Then** it is Available.
2. **Given** a node C requiring A and B with completed-set {A}, **When** C's state is queried, **Then** it is Locked.
3. **Given** completed-set {A,B}, **When** C's state is queried, **Then** it is Available.
4. **Given** a node whose id is in the completed-set, **When** its state is queried, **Then** it is Completed (regardless of prerequisites).

### User Story 2 - Mark complete and cascade unlocks (Priority: P1)

The host marks a node complete; its id is added to the completed-set collection, and every node whose
prerequisites are now all satisfied transitions to Available. Marking an already-completed node changes
nothing.

**Why this priority**: This is the engine in motion — completing a node unlocks its dependents, which is
the whole point of a progression DAG. US1 + US2 are the MVP.

**Independent Test**: DAG A,B→C. Mark A complete → C stays Locked (B missing). Mark B complete → C becomes
Available. Mark A again → no change, no events.

**Acceptance Scenarios**:

1. **Given** DAG A,B→C and A already complete, **When** the host marks B complete, **Then** C becomes Available.
2. **Given** a node already in the completed-set, **When** the host marks it complete again, **Then** the completed-set and all node states are unchanged and no events fire.
3. **Given** a node marked complete, **When** the completed-set is inspected, **Then** it contains that node's id.

### User Story 3 - Events on state change (Priority: P2)

The evaluator raises events when nodes change state: a node becoming Available, and a node becoming
Completed. The initial evaluation raises Available for every initially-available node.

**Why this priority**: Lets the host react (unlock UI, reveal content) without polling; US1+US2 already
work via queries, so events can land second.

**Independent Test**: Subscribe; initialize a DAG A,B→C → Available fires for A and B (not C). Mark A,B →
Completed fires for A then B, Available fires for C exactly once.

**Acceptance Scenarios**:

1. **Given** subscribers, **When** the evaluator initializes over A,B→C, **Then** OnNodeAvailable fires for A and B and not for C.
2. **Given** A complete, **When** B is marked complete, **Then** OnNodeCompleted fires for B and OnNodeAvailable fires once for C.
3. **Given** a re-mark of an already-completed node, **When** it happens, **Then** no event fires.

### User Story 4 - Durable and reversible via the completed-set (Priority: P2)

Because the completed-set is a graphcore P2 collection on the shared context, completion **persists**
(save) and **history-restores** (step-back). After the context's completed-set shrinks (a step-back, or an
explicit un-complete), re-evaluation reports the smaller satisfied set — "back" is a **re-pass**, not an
undo of side-effects; re-evaluation is idempotent.

**Why this priority**: Reuses P2's durability so progress survives save/undo for free, and demonstrates the
reactive "back = re-pass" semantics. Valuable but US1–US3 are the functional core.

**Independent Test**: Mark A,B,C complete (C Available→Completed). Restore the context to a snapshot where
the completed-set was {A}. Re-evaluate → B Available, C Locked again; no node "undo" side-effects ran.

**Acceptance Scenarios**:

1. **Given** completed-set {A,B} and C Available, **When** the context is restored to completed-set {A} and the evaluator re-evaluates, **Then** C is Locked and B is Available.
2. **Given** any sequence of completions and restores, **When** the evaluator re-evaluates, **Then** node states depend only on the current completed-set (idempotent derivation), never on history of side-effects.

### Edge Cases

- **Empty graph / a node with no prerequisites** → that node is Available from initialization.
- **A node id appears in the completed-set but not in the graph** → ignored for state derivation (no crash).
- **Cyclic prerequisites** (A→B→A) → the engine does not traverse, so it does not infinite-loop; such mutually-dependent nodes simply never become Available (documented; cycle prevention is the author's concern, not a crash).
- **Mark a node complete whose prerequisites are NOT met** → completion is host-driven and allowed; the node becomes Completed and its dependents re-evaluate (the engine trusts the host; gating completion is a domain concern).
- **Re-mark / idempotent completion** → no duplicate in the set, no events.
- **Re-evaluate with no changes** → no events.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A reactive evaluator MUST be initializable over a graphcore graph + context + a named completed-set collection key.
- **FR-002**: Node state MUST be derived as: Completed if the node id is in the completed-set; else Available if the node has no prerequisites OR all prerequisite nodes are Completed; else Locked. Prerequisites of a node are the source nodes of its incoming edges.
- **FR-003**: Marking a node complete MUST add its id to the completed-set collection and trigger re-evaluation; marking an already-completed node MUST be a no-op (no duplicate, no events).
- **FR-004**: Re-evaluation MUST emit a "node available" event when a node transitions to Available and a "node completed" event when a node transitions to Completed; no event fires for a state that did not change.
- **FR-005**: Initialization MUST emit "node available" for every node Available at that point.
- **FR-006**: The evaluator MUST expose queries: a node's current state, the set of Available node ids, and the set of Completed node ids.
- **FR-007**: The evaluator MUST NOT be a traversal: no single "current node", no Proceed/Choose/Start→End; state is derived from the completed-set and topology.
- **FR-008**: Re-evaluation MUST be idempotent and reversible: node states depend only on the current completed-set, so a shrunk set (after step-back or un-complete) yields the corresponding smaller satisfied set; "back" never runs node undo side-effects.
- **FR-009**: The completed-set MUST be a graphcore P2 collection on the shared context, so completion persists (save) and history-restores; the evaluator MUST be able to re-evaluate from a restored context (a public re-evaluate entry point).
- **FR-010**: graphcore MUST be unchanged. The engine MUST live in a NEW library `com.faolline.graphstandard` (version 0.1.0) that depends on graphcore 0.5.0; this feature creates that library (package + runtime + test assemblies) minimally.
- **FR-011**: The engine MUST encode only universal reactive-DAG semantics (prerequisite satisfaction, availability, completion) — zero domain vocabulary.
- **FR-012**: The capability MUST be headless (no MonoBehaviour/UnityEvent; C# events) and verifiable in EditMode, including a progression-DAG scenario.

### Key Entities

- **Reactive node state**: one of Locked / Available / Completed, derived per node.
- **Prerequisite**: a source node of an incoming edge; a node's prerequisites must all be Completed for it to be Available (AND).
- **Completed-set**: a graphcore P2 string-set collection holding the ids of completed nodes; the durable serialization of progress.
- **ReactiveEvaluator**: the cursor-less engine that derives states, cascades unlocks, and emits events.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a DAG A,B→C, C is Locked until both A and B are completed, then Available; completing only A leaves C Locked.
- **SC-002**: The entire existing graphcore + graphTest EditMode suite (586 tests) passes unchanged, and graphstandard's own EditMode tests pass.
- **SC-003**: Completing a node emits exactly the correct availability/completion events (the right cascade, none spurious, idempotent on re-mark).
- **SC-004**: After restoring the context to a smaller completed-set and re-evaluating, the engine reports the corresponding smaller satisfied set (reversible / re-pass), with no node side-effects executed.
- **SC-005**: graphcore's Runtime is untouched by this feature (no foundation change); graphstandard is a standalone 0.1.0 lib depending on graphcore 0.5.0.
- **SC-006**: A progression-DAG scenario mirroring the example game (prerequisite puzzles gating a later one) is demonstrated headless in graphstandard's tests.

## Assumptions

- **Prerequisites = incoming-edge sources, AND-combined.** A node needs ALL prerequisites Completed. Generic threshold / OR / N-of-M is the P4 Join (deferred).
- **Completion is host-driven** (`MarkCompleted`). Condition-driven auto-completion (a node completing when a context condition holds) is deferred.
- **One completed-set collection per evaluator**, named at initialization.
- **Re-evaluation trigger (MVP)**: explicit — on `MarkCompleted` and on a public re-evaluate call (used by the host after a step-back/restore). Auto-subscribing the evaluator to the completed-set's P2 change notification is an available enhancement, not required for the MVP.
- **The engine trusts the host** about when completion is legal (it does not itself gate completion on prerequisites); gating is a domain (questsystem) concern.
- **graphstandard hosts the engine now**; standard nodes and the Flow engine come later. Promoting starterGraph's set/compare nodes is a separate future feature.
- **Governance**: EditMode TDD; lib-appropriate `[GraphStandard]` log prefix; one class per file; XML docs on new public API; new lib at 0.1.0.

## Out of Scope *(deferred)*

- The generic **threshold-Join** node (P4 — OR / N-of-M); the MVP uses AND-of-incoming-edges only.
- The **Flow / multi-active** engine (re-pass with fork, one-shot "visited" mark).
- The **Time** node (P5) and the resolution-ordering / priority policy (axis A).
- **Condition-driven completion** (auto-complete on a context condition).
- **Promoting starterGraph's standard nodes** into graphstandard; any **visual/editor authoring** of reactive graphs; the **questsystem** domain lib itself.
