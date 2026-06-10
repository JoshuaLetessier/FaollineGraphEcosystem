# Feature Specification: ReactiveEvaluator re-lock event + reactive-hosting doc clarity (slice 7)

**Feature Branch**: `026-reactive-relock-event`

**Created**: 2026-06-11

**Status**: Draft

**Input**: Round-4 dogfooding quick wins. W2: `ReactiveEvaluator` raises only `OnNodeAvailable`/`OnNodeCompleted`
— there is no symmetric "became Locked" event, so UI cannot react to a node re-locking on a reset/step-back
without a manual repaint; the event surface feels asymmetric. W1: the graphstandard README shows both
`MarkCompleted` and the `OnCollectionChanged → Reevaluate` bridge, and a real consumer needed a re-read to
realise they are **alternatives** (calling `MarkCompleted` already re-derives — adding the bridge double-evaluates).

## Overview

Round 4 (a naive consumer built a 3-puzzle escape room with a live "2-of-3 unlocks the exit" rule) reported
**no library bugs** and that the progression was "almost free". Two small, clearly-correct refinements remain:

- **W2 — a re-lock event.** `ReactiveEvaluator` emits `OnNodeAvailable` and `OnNodeCompleted` but nothing when a
  node returns to `Locked` (which happens on `Reevaluate` after the completed-set shrinks — a replay/step-back).
  Add a symmetric `OnNodeLocked` so a host can react to re-locking the same way it reacts to unlocking.
- **W1 — documentation clarity.** Make the README lead with the simplest completion path (`MarkCompleted`, which
  re-derives internally) and state plainly that the `OnCollectionChanged → Reevaluate` bridge is the **alternative**
  for when a Linear-flow action writes the set — *not both*, or completion double-evaluates.

Both are additive/non-breaking. graphstandard MINOR bump; graphcore and gameflow untouched.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - React to a node re-locking (Priority: P1) 🎯 MVP

A host drives UI from the evaluator. When the player resets (or steps back) and the completed-set shrinks below
a node's threshold, the node returns to `Locked`; the host wants an event for that, symmetric to the unlock
event, instead of inferring it.

**Why this priority**: It closes the event-surface asymmetry the consumer flagged and is the only code change in
the slice.

**Independent Test**: subscribe to the new event; drive a node Locked→Available (record prerequisites) then
Available→Locked (`Reevaluate` after removing a prerequisite) — the re-lock event fires for that node on the
backward transition, and not spuriously while it stays Available.

**Acceptance Scenarios**:

1. **Given** a k-of-N node currently `Available`, **When** the completed-set drops below `k` and `Reevaluate`
   runs, **Then** the new re-lock event fires for that node (and its state is `Locked`).
2. **Given** a subscriber to all three events, **When** `Start()` performs the initial emission, **Then** every
   currently-`Locked` node raises the re-lock event once (symmetric with the existing initial Available/Completed
   emission), and Available/Completed nodes do not raise it.
3. **Given** a node that stays `Available` across a `Reevaluate`, **When** re-evaluation runs, **Then** the
   re-lock event does not fire for it (events fire only on transitions / initial emission, as today).

### User Story 2 - Understand which completion path to use (Priority: P2)

A consumer reading the README must immediately see how to record completion without conflating the two paths.

**Why this priority**: Documentation-only; removes the single re-read the consumer reported.

**Independent Test**: the "Hosting a reactive progression" section leads with `MarkCompleted` and contains an
explicit "call `MarkCompleted` **or** bridge an action's write with `OnCollectionChanged → Reevaluate` — not both"
note.

**Acceptance Scenarios**:

1. **Given** the README hosting section, **When** a reader follows it, **Then** the primary path shown is owning
   the evaluator and calling `MarkCompleted`, with the action+bridge presented as the alternative for when the
   flow itself writes the set, and an explicit not-both caveat.

### Edge Cases

- **Initial emission**: `Start()` raising the re-lock event for all initially-Locked nodes is intended
  (symmetry), not noise — there are no prior subscribers to a brand-new event.
- **Idempotent derivation**: a `Reevaluate` that produces no transition for a node raises no event for it.
- **No double-evaluate**: documentation makes clear `MarkCompleted` already re-derives; the bridge is only for
  the action-writes-the-set path.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ReactiveEvaluator` MUST expose a symmetric event raised when a node enters the `Locked` state,
  named consistently with the existing `OnNodeAvailable` / `OnNodeCompleted` events and carrying the node id.
- **FR-002**: The re-lock event MUST fire on a backward transition to `Locked` during `Reevaluate`, and once per
  currently-Locked node during the initial emission in `Start()`; it MUST NOT fire for a node whose state is
  unchanged by a `Reevaluate`.
- **FR-003**: The existing `OnNodeAvailable` / `OnNodeCompleted` behavior, the derivation logic, and all other
  public members MUST be unchanged (the addition is purely additive; existing subscribers are unaffected).
- **FR-004**: The graphstandard README's reactive-hosting section MUST lead with the `MarkCompleted` path and
  state explicitly that `MarkCompleted` and the `OnCollectionChanged → Reevaluate` bridge are alternatives —
  not to be used together (double-evaluation) — and document the new re-lock event.
- **FR-005**: graphcore and gameflow MUST be unchanged. graphstandard MUST stay append-only/source-compatible
  and bump a MINOR version. The existing test suites MUST stay green; the new event MUST have EditMode coverage.
- **FR-006**: Dev standards — one member added with XML docs; `[GraphStandard]` conventions; CHANGELOG updated.

### Key Entities

- **Re-lock event**: an `Action<string>` on `ReactiveEvaluator`, the Locked-state counterpart of the existing
  Available/Completed events.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A host can subscribe to a single event and be notified whenever a node re-locks, symmetric with
  unlock notification — verified by an EditMode test driving Locked→Available→Locked.
- **SC-002**: The README hosting section presents `MarkCompleted` as the primary path with an explicit not-both
  caveat and documents the re-lock event; no reader needs to infer that the two completion paths are exclusive.
- **SC-003**: graphcore/gameflow untouched; existing suites stay green; graphstandard ships the new MINOR.

## Assumptions

- **Symmetric initial emission is desired.** Mirroring `Start()`'s existing initial Available/Completed emission,
  the re-lock event also fires for initially-Locked nodes; this is intentional symmetry, harmless to a fresh
  subscriber.
- **W3 stays out.** The "gate a Linear signal on a reactive node's availability" need is acknowledged but
  deliberately deferred to a separate, deeper discussion — this slice is only the two quick wins.

## Out of Scope *(deferred)*

- **W3** — making "this signal is only acceptable while this reactive node is Available" first-class (reactive
  availability mirrored to the context, or a conditional signal-await in the driver). To be discussed separately.
- Any change to graphcore or gameflow; the collection primitives; save/load.
