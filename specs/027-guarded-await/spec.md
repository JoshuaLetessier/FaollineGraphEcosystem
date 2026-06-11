# Feature Specification: guarded await — re-armable signal resume conditions (slice 8)

**Feature Branch**: `027-guarded-await`

**Created**: 2026-06-11

**Status**: Draft

**Input**: Round-4 dogfooding finding W3 (the one real seam), option (ii). A consumer building an escape room had
to hand-wire `if (IsExitOpen) RaiseSignal("exit")` — reading the Reactive engine's availability to gate the
Linear engine's signal. Today a node parked on an await-signal resumes on a **name match alone**; there is no
way to say "accept this signal only while the world is in a given state". The general capability: an awaiting
node may carry optional **resume conditions** so a raised signal resumes it only when those conditions pass —
and, crucially, when they don't, the signal is **ignored and the node stays parked** (re-arm), so the actor can
try again once the world is ready.

## Overview

The await/signal park lives in the graphcore substrate (`BaseRunner`): a node with an await-signal name holds
until `RaiseSignal(name)` delivers the matching name. This slice adds an **optional gate** on that resume: a
node may declare resume conditions (universal `BaseCondition`s over the context); a matching signal resumes the
node **only if all of them pass**, otherwise the signal is ignored and the node remains parked. This is the
re-arm semantics that "press the button anytime, it only acts when the world is ready" needs — present in roughly
half of real player→world interactions (locked door / has-key, talk / quest-accepted, buy / enough-gold, craft /
has-ingredients, start / players-ready, skip / skippable…). It is intentionally **universal** and lives in the
substrate.

A companion ergonomic addition lets the same gate be authored from the code-first `GraphBuilder`
(`ResumeWhen(...)`), since that is how consumers build graphs today. graphcore `0.6.0 → 0.7.0`; graphstandard
`0.6.0 → 0.7.0` (builder sugar); gameflow untouched (the driver re-exposes `RaiseSignal`; the gate is enforced
by the runner).

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Graph author** — parks a node on a signal and attaches resume conditions, so the signal only advances the
  flow when the context satisfies them.
- **The actor raising the signal** (player input, host code) — may raise the signal at any time; it takes effect
  only when the gate passes.

### User Story 1 - A signal resumes only when the world is ready (Priority: P1) 🎯 MVP

A node is parked awaiting a signal and carries a resume condition. Raising the signal while the condition is
false does nothing (the node stays parked); raising it once the condition is true advances the flow.

**Why this priority**: This is the whole capability — re-armable, condition-gated signal resume.

**Independent Test**: park on an await node with a resume condition reading a context value; raise the matching
signal while the condition is false → still `WaitingForSignal`, no advance; make the condition true, raise again
→ the node exits and the flow advances.

**Acceptance Scenarios**:

1. **Given** a parked await node whose resume condition is false, **When** the matching signal is raised, **Then**
   the runner stays `WaitingForSignal` and does not advance (the signal is ignored, not consumed-and-stuck).
2. **Given** the same node after the condition becomes true, **When** the matching signal is raised again, **Then**
   the node exits and the flow advances (re-arm: the earlier ignored raise did not break anything).
3. **Given** multiple resume conditions, **When** the signal is raised, **Then** the node resumes only if **all**
   pass (AND), consistent with how entry conditions combine.

### User Story 2 - No resume conditions behaves exactly as before (Priority: P1)

A node with an await-signal name and **no** resume conditions resumes on a name match alone — byte-for-byte the
current behavior.

**Why this priority**: Append-only guarantee for the foundation; every existing await flow must be unchanged.

**Independent Test**: an await node with an empty resume-condition list resumes immediately on the matching
signal, exactly as today; a non-matching signal name is still ignored.

**Acceptance Scenarios**:

1. **Given** an await node with no resume conditions, **When** the matching signal is raised, **Then** it resumes
   immediately (current behavior).
2. **Given** a wrong signal name, **When** it is raised, **Then** the node stays parked regardless of conditions.
3. **Given** a null entry in the resume-condition list, **When** the signal is raised, **Then** the null is
   skipped (a warning), not treated as a failed gate — consistent with entry-condition tolerance.

### User Story 3 - Author the gate from the code builder (Priority: P2)

A consumer building graphs with the fluent builder attaches resume conditions to an await node without manually
touching the node's condition list.

**Why this priority**: Ergonomics — the capability must be reachable through the authoring path consumers
actually use (round-4 built its shell with the builder).

**Independent Test**: `builder.AddStatement(...).Await("exit").ResumeWhen(cond)` produces a node whose resume
gate is `cond`; running it reproduces US1.

**Acceptance Scenarios**:

1. **Given** a node built with `Await(name).ResumeWhen(cond)`, **When** the graph runs, **Then** the signal-gate
   behaves as in US1.

### Edge Cases

- **Signal ignored, not consumed**: a raise that fails the gate leaves the node parked and re-armable; it is not
  an error and does not advance or get "stuck with nowhere to go" (the key difference from gating an outgoing
  edge, which consumes the signal first).
- **Signal payload still recorded**: raising a signal records it on the context as today (so a resume condition
  could even read the just-raised payload); only the *resume* is gated.
- **Host override unaffected**: a direct host advance (`Advance` / forced GoTo) is an explicit override and is
  **not** gated by resume conditions (it already bypasses condition evaluation) — resume conditions gate
  signal-driven resume only.
- **Empty / all-pass**: no conditions, or all conditions pass → immediate resume.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A node MUST be able to carry an optional, ordered list of resume conditions (universal
  `BaseCondition`s), defaulting to empty, alongside its await-signal name.
- **FR-002**: When a node is parked awaiting a signal, a raised signal whose name matches MUST resume the node
  **only if every resume condition passes** against the context; if any fails, the raise MUST be ignored and the
  node MUST remain parked (re-arm), with no advance and no error.
- **FR-003**: Resume conditions MUST combine as AND and tolerate a null entry by skipping it with a warning,
  consistent with entry-condition evaluation.
- **FR-004**: A node with no resume conditions MUST resume on a name match alone — identical to the current
  behavior; a non-matching signal name MUST still be ignored regardless of conditions.
- **FR-005**: Resume conditions MUST gate only signal-driven resume; an explicit host advance/override MUST NOT
  be gated by them.
- **FR-006**: The code-first builder MUST offer a fluent way to append resume conditions to a node.
- **FR-007**: graphcore MUST stay append-only/source-compatible — only a new optional node field and the gated
  resume path are added; `AwaitSignalName`, `EntryConditions`, `RaiseSignal`, and all other members are
  unchanged; pre-existing assets (no resume conditions) behave identically. graphcore MUST bump a MINOR.
- **FR-008**: gameflow MUST be unchanged (the driver's `RaiseSignal` already routes through the runner, which
  enforces the gate). graphstandard MUST stay append-only and bump a MINOR for the builder sugar.
- **FR-009**: All existing graphcore + graphstandard + gameflow suites MUST stay green; the new gate and builder
  sugar MUST have EditMode coverage.
- **FR-010**: Dev standards — `[GraphCore]` / `[GraphStandard]` prefixes; XML docs on the new field, the gated
  path, and the builder method; CHANGELOG + README updated for both packages.

### Key Entities

- **Resume conditions**: an optional list of `BaseCondition` on a node, the gate a matching await-signal must
  pass to resume the parked node.
- **Re-arm**: the property that an ignored (gate-failing) raise leaves the node parked and retriable, rather than
  consuming the signal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An author can park a node on a signal and have it resume only while the context satisfies declared
  conditions, with the signal harmlessly retriable until then — expressed entirely in the graph, no host glue.
- **SC-002**: Every await flow with no resume conditions behaves exactly as before; existing suites stay green.
- **SC-003**: The gate is authorable through the code builder.
- **SC-004**: graphcore and graphstandard ship the new MINORs; gameflow untouched; the escape-room "open the exit
  only when 2-of-3 are done" gate is now expressible in-graph (await `exit` guarded by a count-at-least
  condition), removing the consumer's hand-wired signal gate.

## Assumptions

- **Re-arm (ignore), not latch.** A gate-failing raise is ignored; the actor re-raises later. We do not queue/latch
  the signal to auto-fire when the gate later passes (simpler, matches the consumer's existing "only raise when
  ready" usage; a latch can be added later if a real need appears).
- **AND semantics**, mirroring entry conditions, is the expected combination; OR is composed with a custom
  condition if ever needed.
- **The gate lives in the substrate.** Signal-await is graphcore's; the gate belongs next to it, as a universal
  capability, not in a host layer.

## Out of Scope *(deferred)*

- Authoring resume conditions from the **gameflow visual inspector** (the code builder covers the consumer's
  path; a visual field is a later ergonomic follow-up).
- Latching/queuing a gate-failing signal to fire automatically when the gate later passes.
- Any change to gameflow runtime, to the Reactive/Flow engines, or to the time-wait path.
- A symmetric gate on time-waits or on entry (entry already has `EntryConditions`).
