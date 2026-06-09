# Feature Specification: P5 — Time (host-fed wait / timeout)

**Feature Branch**: `018-time-wait`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "P5 — give the Linear runner a notion of time: a node can hold for a duration, and the host advances it by feeding elapsed seconds via Tick(dt). The runner owns NO clock — pausing is just not ticking, slow-mo is scaling dt. Mirrors P1 await-signal (hold-then-resume) but the resume trigger is elapsed time. graphcore 0.5.0 → 0.6.0, append-only; default (no duration) unchanged."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Graph author** — marks a node to pause for a duration (a loading beat, a cinematic gap, a hint delay).
- **Host integrator** — drives the graph and feeds elapsed time each frame via a tick.

### User Story 1 - A node holds for a duration, advanced by host-fed time (Priority: P1) 🎯 MVP

A graph author can give a node a **wait duration**. On entry the runner **holds** at that node (it does not
advance); the host calls a **tick** with the elapsed seconds each frame; when the accumulated time reaches
the duration, the runner advances normally. The runner owns no clock — it only consumes the time the host
feeds it.

**Why this priority**: This is the whole feature — timed transitions for scene-flow/cinematics/hints. The
host-fed model makes pause (don't tick), slow-motion (scale dt), and fast-forward fall out for free.

**Independent Test**: start → [wait 2.0s] → end. Run; the runner reports it is waiting and does not end.
Tick(1.0) → still waiting. Tick(1.0) → advances and ends.

**Acceptance Scenarios**:

1. **Given** execution at a node with wait duration 2.0, **When** no time has been fed, **Then** the runner reports it is waiting and has not advanced.
2. **Given** the node is waiting on 2.0 and 1.0s has been ticked, **When** another 1.0s is ticked, **Then** the runner advances along the node's outgoing edge(s).
3. **Given** a node with no wait duration (0), **When** entered, **Then** behavior is identical to today (no hold).
4. **Given** a wait of 2.0, **When** a single tick of 5.0s is fed, **Then** the runner advances (the overshoot still satisfies the wait).

### User Story 2 - Pause, slow-motion, and fast-forward via the host (Priority: P2)

Because the runner consumes only host-fed time, the host controls the flow of time: not ticking pauses a
wait indefinitely; feeding a scaled dt slows or speeds it; feeding a large dt fast-forwards it. No special
runner API is needed for these.

**Why this priority**: It is the practical payoff of the host-fed model and must be demonstrably true.

**Independent Test**: a node waiting on 3.0 — ticking 0 repeatedly never advances (pause); ticking 0.5·dt
takes twice as long; ticking once with 10.0 advances immediately.

**Acceptance Scenarios**:

1. **Given** a node waiting on 3.0, **When** Tick(0) is called any number of times, **Then** it never advances (paused).
2. **Given** a node waiting on 3.0, **When** time is fed in fractional increments, **Then** it advances exactly when the cumulative fed time reaches 3.0.

### Edge Cases

- **Wait duration 0 (or negative)** → no hold; the node behaves as a normal node (no timed pause).
- **Tick called when not waiting on time** → no effect (a no-op; it does not advance signal-waits or ready nodes).
- **A node configured with BOTH a wait duration and an await-signal** → the await-signal takes precedence (signal-wait); the duration is ignored for that node (documented).
- **Step-back into a timed node** → the wait re-arms (the countdown restarts from the full duration).
- **Manual advance while time-waiting** → Proceed/ChooseById are no-ops while waiting on time (only elapsed time advances it).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A node MUST be able to declare a non-negative **wait duration** (seconds). Entering such a node MUST hold execution there instead of advancing.
- **FR-002**: The runner MUST expose a **tick** that consumes elapsed seconds; when the cumulative fed time for the held node reaches its duration, the runner MUST advance using the existing edge-selection rules.
- **FR-003**: The runner MUST own no internal clock — it advances a time-wait ONLY in response to host-fed time (so pause = no tick, slow-mo = scaled dt).
- **FR-004**: A wait duration of 0 (or negative) MUST NOT hold — behavior identical to the pre-feature node.
- **FR-005**: A tick fed when the runner is not waiting on time MUST be a safe no-op.
- **FR-006**: When a node declares both a wait duration and an await-signal, the **await-signal takes precedence**; the wait duration is ignored for that node.
- **FR-007**: `Proceed`/`ChooseById` MUST be inert while waiting on time (only elapsed time advances it).
- **FR-008**: Returning to a timed node via step-back MUST re-arm its wait (restart the countdown).
- **FR-009**: When no node declares a wait duration and the host never ticks, runtime behavior MUST be identical to the pre-feature (0.5.0) behavior (full back-compat).
- **FR-010**: All additions MUST be append-only — no existing public signature/field/behavior removed or changed (semver MINOR, graphcore 0.5.0 → 0.6.0).
- **FR-011**: The capability MUST be headless (no MonoBehaviour/UnityEvent; pure C#), verifiable in EditMode.

### Key Entities

- **Wait duration**: a per-node non-negative number of seconds the runner holds on entry before advancing.
- **Tick**: the host-fed elapsed-time input that drives a held timed node toward its duration.
- **Waiting-for-time state**: the runner state while a timed node is holding.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A graph holding on a 2.0s node advances solely because the host fed ≥ 2.0s of ticks — no other input.
- **SC-002**: The entire existing 612-test suite passes unchanged (no wait = identical to 0.5.0).
- **SC-003**: Pause (Tick 0), slow-mo (scaled dt), and fast-forward (large dt) all behave correctly with no extra API.
- **SC-004**: A node with no wait duration exhibits zero observable difference from 0.5.0.
- **SC-005**: Step-back into a timed node re-arms the countdown.

## Assumptions

- **Host-fed time, runner owns no clock** — the host calls a tick with elapsed seconds (typically per frame).
  Pause/slow-mo/fast-forward are host responsibilities (scale or withhold dt).
- **Await-signal precedence** when both a duration and a signal are set on one node.
- **Re-arm on re-entry** (step-back restarts the countdown). Persisting a partial countdown across save is a
  future enhancement, not in this MVP.
- **Mirrors P1**: the same hold-then-resume shape as await-signal, with elapsed time as the resume trigger.
- **Governance**: EditMode TDD; `[GraphCore]` prefix; one class per file; XML docs; append-only / semver MINOR.

## Out of Scope *(deferred)*

- Persisting a partial countdown across save/restore (re-arm-on-re-entry only).
- Absolute wall-clock / calendar scheduling (a host concern that reduces to raising a signal — see P1).
- Time in the Reactive/Flow engines (this is a Linear-runner capability); per-node timeouts that fall
  through to an alternate edge (a later enhancement); an authoring inspector for durations.
