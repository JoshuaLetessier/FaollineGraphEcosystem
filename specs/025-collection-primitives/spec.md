# Feature Specification: graphstandard universal collection primitives + reactive-hosting pattern (slice 6)

**Feature Branch**: `025-collection-primitives`

**Created**: 2026-06-10

**Status**: Draft

**Input**: User description: "Promote the universal collection primitives into graphstandard so a Linear flow can
WRITE to and GATE on a graphcore string-set collection, and document the pattern for HOSTING a ReactiveEvaluator
on a shared context. The ReactiveEvaluator already derives k-of-N unlocks from a completed-set; the
GraphFlowDriver.Boot(context, registry) seam already lets a consumer share the driver's context. The only
missing universal bricks are a node ACTION that adds a value to a collection and CONDITIONS that read one —
today those exist only in graphTest (test-side, not the real lib)."

## Overview

Three prior dogfooding rounds matured the gameflow runtime; slice 5 opened the `Boot(context, registry)` seam so
a consumer can run the Linear flow on a context they prepared. The remaining gap before a real
**progression** can be assembled is that graphstandard — the domain-neutral standard-nodes lib — has **no
authorable way to write to or read a collection**. The `ReactiveEvaluator` (graphstandard) already derives
k-of-N unlocks from a *completed-set* collection on any `BaseContext`, and the underlying context already
exposes `AddToCollection` / `CollectionContains` / `CollectionCount` / `OnCollectionChanged`. But the only
collection **action** and **conditions** live in `graphTest` (test fixtures), so a consumer who wants a Linear
node to record completion, or a Linear edge to gate on "k done", must hand-write a custom action/condition.

This slice promotes three **universal** primitives into graphstandard — one collection-writing action and two
collection-reading conditions — and documents the **reactive-hosting pattern** they complete: a Linear flow
records completion via the action, a `ReactiveEvaluator` over the **same** shared context derives the unlocks,
bridged by a two-line `OnCollectionChanged → Reevaluate` subscription. graphstandard bumps a MINOR (additive);
graphcore and gameflow are untouched.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Graph author** — attaches a standard action to a node and standard conditions to edges in the editor, with
  no custom scripting, to record and read membership in a collection.
- **Game integrator** — hosts a `ReactiveEvaluator` on the driver's shared context so that completing
  k-of-N prerequisites (recorded by the action) unlocks a downstream node.

### User Story 1 - Record membership from a node (Priority: P1) 🎯 MVP

A graph author wants a node, when entered (or exited), to record a value into a named collection on the shared
context — e.g. marking that a step is done — without writing any code.

**Why this priority**: Writing to the collection is the foundation; the conditions and the reactive pattern all
read what this records.

**Independent Test**: attach the add-to-collection action to a node with a configured key and value; run the
flow through that node; the context's collection at that key now contains the value, and re-entering the node
does not duplicate it.

**Acceptance Scenarios**:

1. **Given** a node carrying the add-to-collection action configured with key `K` and value `V`, **When** the
   flow enters that node, **Then** the context's collection `K` contains `V`.
2. **Given** the same node entered twice, **When** the action runs again, **Then** `V` appears once (the
   underlying set is idempotent — no duplicate).
3. **Given** the action with an empty key or empty value, **When** it runs, **Then** it makes no change and the
   flow continues (a graceful, configuration-tolerant no-op).

### User Story 2 - Gate an edge on collection state (Priority: P1)

A graph author wants an edge to be taken only when a collection contains a specific value, or only when a
collection has reached a count threshold (k-of-N), without writing code.

**Why this priority**: Reading the collection is the other half of authoring with collections; the count
threshold is exactly how a "k done unlocks this" gate is expressed in a Linear flow.

**Independent Test**: place the contains-condition (key `K`, value `V`) on an edge — it is satisfied exactly
when `K` contains `V`; place the count-at-least condition (key `K`, threshold `N`) on an edge — it is satisfied
exactly when `K` holds at least `N` distinct values.

**Acceptance Scenarios**:

1. **Given** the contains-condition for (`K`,`V`), **When** evaluated against a context whose `K` contains `V`,
   **Then** it is satisfied; when `K` lacks `V`, **Then** it is not satisfied.
2. **Given** the count-at-least condition for (`K`, threshold `N`), **When** `K` holds `≥ N` values, **Then** it
   is satisfied; with fewer than `N`, **Then** it is not satisfied.
3. **Given** a threshold of `0` (or a key that has no collection yet), **When** the count-at-least condition is
   evaluated, **Then** the result is well-defined (a count of `0` satisfies a threshold of `0` and fails any
   positive threshold).

### User Story 3 - Host a reactive progression on the shared context (Priority: P1)

A game integrator records completion of prerequisite nodes via the add-to-collection action and wants a
downstream node to become *available* once a k-of-N threshold of its prerequisites is recorded — derived live
by a `ReactiveEvaluator` running on the **same** context the Linear flow runs on.

**Why this priority**: This is the growth path the slice exists to unblock; it proves the primitives + the
existing reactive engine + the boot seam compose into a working progression without any new bespoke code.

**Independent Test**: a graph where prerequisite nodes carry the add-to-collection action writing their ids into
a *completed-set*; a `ReactiveEvaluator` over the same context (with a `k` required count for the downstream
node and an `OnCollectionChanged → Reevaluate` bridge) reports the downstream node as **available** exactly once
`k` of its prerequisites have been recorded — not before.

**Acceptance Scenarios**:

1. **Given** a downstream node requiring `k` of its `N` prerequisites and a reactive evaluator bridged to the
   completed-set, **When** fewer than `k` prerequisites have been recorded, **Then** the downstream node is not
   yet available.
2. **Given** the same setup, **When** the `k`-th prerequisite is recorded by the action, **Then** the evaluator
   re-derives and the downstream node becomes available (its availability event is raised).
3. **Given** the pattern documented in the quickstart, **When** an integrator follows it, **Then** the only
   non-authoring code they write is the two-line collection-change bridge (no custom action or condition).

### Edge Cases

- **Empty/whitespace key or value** on the action → no-op, flow continues (US1-3).
- **Count-at-least threshold of 0** → satisfied even for an absent/empty collection (US2-3).
- **Re-recording an already-present value** → no duplicate, and (in the reactive pattern) no spurious
  re-derivation beyond what an idempotent set implies.
- **A condition referencing a key with no collection yet** → treated as an empty collection (contains = false,
  count = 0), never an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: graphstandard MUST provide an authorable node **action** that adds a configured value to a
  configured collection key on the run context, usable on any node's enter or exit list.
- **FR-002**: The add-to-collection action MUST be idempotent with respect to the underlying string-set (adding
  an already-present value leaves a single occurrence) and MUST be a graceful no-op when its key or value is
  empty.
- **FR-003**: graphstandard MUST provide an authorable **condition** that is satisfied exactly when a configured
  collection key contains a configured value.
- **FR-004**: graphstandard MUST provide an authorable **condition** that is satisfied exactly when a configured
  collection key holds at least a configured threshold count of values; a threshold of `0` is satisfied by an
  absent or empty collection.
- **FR-005**: The two conditions MUST treat a key with no collection as an empty collection (contains = false,
  count = 0) rather than failing.
- **FR-006**: The action and both conditions MUST be authorable as assets (creatable from the editor's asset
  menu) and operate purely through the universal collection API — no domain-specific vocabulary or types.
- **FR-007**: The slice MUST document a **reactive-hosting pattern**: a Linear flow records completion through
  the add-to-collection action into a shared *completed-set*; a reactive evaluator over the **same** shared
  context derives k-of-N unlocks; the flow author bridges writes to re-derivation with a single collection-change
  subscription; and a Linear edge may additionally gate on the same set via the count-at-least condition. The
  pattern MUST require no custom action or condition from the consumer.
- **FR-008**: graphcore and gameflow MUST be unchanged (code + docs). graphstandard MUST stay append-only and
  source-compatible — only new files are added; nothing existing is modified in a breaking way. The existing
  graphcore, graphstandard, and gameflow test suites MUST stay green. graphstandard MUST bump a MINOR version.
- **FR-009**: The graphTest collection fixtures MUST remain in place (they are the test reference); the new
  primitives live in graphstandard and do not delete or alter the graphTest equivalents.
- **FR-010**: Dev standards — `[GraphStandard]` log prefix; one class per file; XML docs on the action and both
  conditions; README + CHANGELOG updated with the three primitives and a short reactive-hosting pattern note.

### Key Entities

- **Add-to-collection action**: an authorable node action carrying a collection key and a value; on run, records
  the value in the context's collection at that key.
- **Collection-contains condition**: an authorable condition carrying a key and a value; satisfied when the
  collection at the key contains the value.
- **Collection-count-at-least condition**: an authorable condition carrying a key and a threshold; satisfied
  when the collection at the key holds at least the threshold count.
- **Completed-set**: the shared string-set collection (on the run context) that the Linear flow writes and the
  reactive evaluator reads — the join point of the hosting pattern.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A graph author can make a node record a value into a named collection, and an edge be taken only
  when that collection contains a value or reaches a count threshold, using only stock graphstandard assets (no
  custom scripting).
- **SC-002**: Recording the same value twice yields a single membership; gating conditions read collection state
  consistently, including the empty/absent-collection and zero-threshold edge cases.
- **SC-003**: An integrator can host a reactive evaluator on the driver's shared context so a downstream node
  becomes available exactly when k-of-N prerequisites are recorded by the action — writing only the two-line
  collection-change bridge, no bespoke action or condition.
- **SC-004**: graphcore and gameflow are untouched; the prior test suites stay green; graphstandard ships the new
  MINOR with README + CHANGELOG covering the primitives and the pattern.

## Assumptions

- **Collections are string-sets.** The graphcore collection API stores distinct string values per key; the
  action's idempotence and the conditions' semantics follow from that (a set, not a multiset/list).
- **The reactive engine already exists and is sufficient.** `ReactiveEvaluator` already derives k-of-N from a
  completed-set and exposes the events and `Reevaluate`; this slice adds only the authorable write/read
  primitives and the documented bridge, not any new engine.
- **The bridge is the consumer's two lines, by design.** Routing a collection write to re-derivation is a single
  `OnCollectionChanged → Reevaluate` subscription the integrator writes; a turnkey wrapper that owns the
  evaluator is deliberately deferred (see Out of Scope).
- **Add only.** Only the add/contains/count primitives the pattern needs are introduced; remove/clear mutators
  and other conditions are not (YAGNI).

## Out of Scope *(deferred)*

- A gameflow **ReactiveProgressionHost** wrapper that owns the evaluator and auto-bridges driver signals to
  `MarkCompleted` (deferred until a dogfood round shows the hand-wired two-line bridge is painful).
- Any change to graphcore or gameflow.
- Additional collection mutators (remove, clear) or further conditions beyond contains / count-at-least.
- Save / load of the completed-set (graphcore collections already persist/restore via the context; no new work
  here).
