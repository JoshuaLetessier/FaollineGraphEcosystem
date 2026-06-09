# Feature Specification: P4 — Generic threshold Join (k-of-N prerequisites)

**Feature Branch**: `017-threshold-join`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "P4 — generalize the Reactive engine's prerequisite rule from ALL (AND) to a configurable threshold k: a node is Available when at least k of its N prerequisites are Completed. AND = k/N (k=N, default), OR = k=1, N-of-M = any k. Extends graphstandard's ReactiveEvaluator (graphstandard 0.1.0 → 0.2.0); graphcore untouched; default AND keeps every P3 test green. Threshold supplied as evaluator configuration (no new node type, no editor UI)."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Graph author / host integrator** — configures, per node, how many of its prerequisites must be Completed for it to unlock.
- **(transitive) questsystem / skill-tree lib developer** — expresses "any 2 of these", "3 of 5 paths", "all of", or "any one".

### User Story 1 - Configurable k-of-N availability (Priority: P1) 🎯 MVP

A node may be given a **required count** k. It becomes **Available** when the number of its **Completed
prerequisites** reaches k (and it is not itself Completed). When no required count is configured for a
node, k defaults to its full prerequisite count N — i.e. **AND**, exactly as today (P3).

**Why this priority**: This is the whole feature — one threshold parameter generalizes the join. The
default-to-AND keeps every existing reactive behavior intact.

**Independent Test**: DAG A,B,C→D. Default (no config): D Available only when A,B,C all Completed. Configure
D with k=2: D Available as soon as any 2 of {A,B,C} are Completed.

**Acceptance Scenarios**:

1. **Given** D requires A,B,C with no configured count, **When** A and B are Completed, **Then** D is Locked (default AND needs all three).
2. **Given** D configured with required count 2, **When** any two of A,B,C are Completed, **Then** D is Available.
3. **Given** D configured with required count 2 and three prerequisites Completed, **When** queried, **Then** D is Available (≥ threshold).
4. **Given** a node with no prerequisites, **When** queried, **Then** it is Available (threshold defaults to 0 of 0).

### User Story 2 - The full join spectrum and its edges (Priority: P1)

One parameter covers every shape: k=N is AND (all), k=1 is OR (any one), 1<k<N is N-of-M. Out-of-range
values are well-defined: k≤0 means the node is ungated (Available unless Completed); k>N means it can never
be made Available by prerequisites alone (it stays Locked until the host completes it directly).

**Why this priority**: Authors rely on the boundary behavior (OR gates, optional/never-auto nodes). It must
be unambiguous and crash-free.

**Independent Test**: D requires A,B,C. k=1 → D Available after the first of A/B/C. k=0 → D Available
immediately. k=4 (>N) → D never becomes Available from prerequisites.

**Acceptance Scenarios**:

1. **Given** D with required count 1, **When** any single prerequisite is Completed, **Then** D is Available (OR).
2. **Given** D with required count 0 (or negative), **When** evaluated with no prerequisites Completed, **Then** D is Available (ungated).
3. **Given** D with required count greater than its prerequisite count, **When** all prerequisites are Completed, **Then** D remains Locked (never auto-available) and no error occurs.
4. **Given** D with required count equal to N, **When** evaluated, **Then** behavior is identical to the default AND.

### User Story 3 - Threshold honored across the whole lifecycle (Priority: P2)

The configured threshold is respected everywhere the engine already acts: state derivation, the unlock
cascade when a prerequisite is completed, the availability/completion events, the initial emission, and the
idempotent/reversible re-evaluation after a step-back.

**Why this priority**: A threshold that only worked for one-shot queries but not for cascades/events would
be useless; this guarantees consistency with all P3 behavior. US1+US2 are the core; this is the integration
guarantee.

**Independent Test**: A "region" node with required count 2 over three member nodes — completing members one
by one fires the region's Available event exactly when the second completes; a step-back that drops a member
re-locks the region.

**Acceptance Scenarios**:

1. **Given** a node with required count 2 over three prerequisites, **When** the second prerequisite is completed, **Then** the node's "available" event fires exactly once.
2. **Given** that node is Available, **When** the completed-set is restored so only one prerequisite remains Completed and the engine re-evaluates, **Then** the node is Locked again (reversible re-pass).
3. **Given** the default (no configured counts), **When** any P3 scenario runs, **Then** results are identical to P3 (AND).

### Edge Cases

- **No required count configured** → defaults to N (AND); existing behavior unchanged.
- **k = 0 or negative** → ungated: Available regardless of prerequisites (unless the node is itself Completed).
- **k > N** → never satisfiable by prerequisites; the node stays Locked until completed by the host directly.
- **Configured count on a node with no prerequisites** → if k ≤ 0 it is Available; if k ≥ 1 it can never auto-unlock (0 prerequisites, needs ≥1).
- **Configured count for a node id not in the graph** → ignored (no crash).
- **A prerequisite id that is also Completed but listed twice / duplicate edges** → counted once (prerequisites are a set).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A node's availability MUST be decidable by a **required count** k: it is Available when the count of its Completed prerequisites is ≥ k and the node is not itself Completed.
- **FR-002**: When no required count is configured for a node, k MUST default to that node's prerequisite count N (AND) — preserving P3 behavior exactly.
- **FR-003**: Required counts MUST be configurable per node and supplied to the evaluator as additive configuration (no graphcore change, no new node type, no editor UI).
- **FR-004**: k = 1 MUST behave as OR (any one prerequisite); k = N as AND; 1 < k < N as N-of-M.
- **FR-005**: k ≤ 0 MUST make the node ungated (Available unless Completed); k > N MUST make the node never auto-available from prerequisites (Locked until host-completed), with no error.
- **FR-006**: The threshold MUST be honored by all engine behaviors: state derivation, the unlock cascade on completion, the availability/completion events, the initial emission, and the idempotent/reversible re-evaluation.
- **FR-007**: A configured count for an unknown node id, or duplicate prerequisites, MUST be handled gracefully (ignored / de-duplicated), never crashing.
- **FR-008**: graphcore MUST be unchanged; the change MUST be confined to `com.faolline.graphstandard` (0.1.0 → 0.2.0, semver MINOR, additive). The existing `ReactiveEvaluator` public surface MUST remain source-compatible (P3 callers keep working with default AND).
- **FR-009**: The capability MUST be universal (a k-of-N threshold over prerequisites) with zero domain vocabulary, headless, and verifiable in EditMode.

### Key Entities

- **Required count (threshold) k**: the minimum number of a node's prerequisites that must be Completed for it to be Available; default = N (AND).
- **Prerequisite set**: the (de-duplicated) source nodes of a node's incoming edges (from P3).
- **ReactiveEvaluator (extended)**: now derives availability from the completed-prerequisite count vs. the per-node threshold.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For D requiring A,B,C with k=2, D is Available after exactly any 2 of the 3 are Completed; with default (no config) D needs all 3.
- **SC-002**: The entire existing 602-test suite (graphcore + graphTest + graphstandard P3) passes **unchanged** — default AND is byte-for-byte the P3 behavior.
- **SC-003**: k=1 unlocks on the first completed prerequisite (OR); k>N never auto-unlocks; k≤0 is ungated — all without errors.
- **SC-004**: The threshold is honored by cascade, events, Start, and Reevaluate (a region with k=2 fires Available exactly when the second member completes, and re-locks on step-back).
- **SC-005**: graphcore is untouched; graphstandard moves 0.1.0 → 0.2.0.
- **SC-006**: A game-like "region Available when ≥ N of its member puzzles are Completed" scenario passes headless.

## Assumptions

- **Threshold is over prerequisite COUNT** (k-of-N incoming edges). Thresholds over arbitrary context
  collections are out of scope (the graphTest count-threshold condition already covers collection counts).
- **Threshold is supplied as evaluator configuration** (a node-id → required-count mapping provided to the
  evaluator), not a serialized node field — keeping graphcore untouched and avoiding editor work. A
  dedicated authorable "Join node" type is deferred.
- **Default = AND** (k = N) when unconfigured, for full P3 back-compatibility.
- **Prerequisites are a set** (duplicate incoming edges counted once), consistent with P3.
- **Governance**: EditMode TDD; `[GraphStandard]` prefix on misuse; one class per file; XML docs on new public API; graphstandard 0.1.0 → 0.2.0.

## Out of Scope *(deferred)*

- The Flow / multi-active engine (fork/join, re-pass, one-shot visited); the Time node (P5); the
  resolution-ordering / priority policy (axis A); condition-driven completion.
- A dedicated serialized **Join node** type with an authoring inspector (thresholds are evaluator
  configuration in this MVP).
- Thresholds over arbitrary context collections beyond prerequisite count; promoting standard nodes into
  graphstandard.
