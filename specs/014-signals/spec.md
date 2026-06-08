# Feature Specification: P1 — Signals (host→runtime event injection)

**Feature Branch**: `014-signals`

**Created**: 2026-06-08

**Status**: Draft

**Input**: User description: "P1 — Signals: add a mechanism to com.faolline.graphcore so the host can push named signals (with optional payload) into a running graph, and authors can make a node wait for / react to a named signal. The push→pull bridge: today BaseRunner is pull-only (Proceed/ChooseById) and no external event can reach a running graph. Root of the engines roadmap (P1). Verified in com.faolline.graphTest. Append-only / semver MINOR / EditMode TDD / headless."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Host integrator** — game/runtime code that drives a graph and observes/raises external events (clicks, item collected, puzzle solved, timer elapsed, drop-on-target).
- **Graph author** — designs a graph and marks where execution should wait for / react to a named event.
- **(transitive) downstream-lib developer** — builds a domain lib (dialogue, quest, …) on graphcore and inherits the capability.

### User Story 1 - Host raises a named signal into a running graph (Priority: P1)

The host can raise a signal identified by a **name**, optionally carrying a **single scalar payload** (bool/int/float/string) or no payload, into a running graph. Every party subscribed to that signal name is notified with the payload. Raising a signal that nobody listens for does nothing and is not an error.

**Why this priority**: This is the irreducible core — the push entry point. Without it, nothing external can reach a graph; every other capability builds on it.

**Independent Test**: Subscribe a listener to `itemCollected`; raise `itemCollected` with payload `"key"`; assert the listener fired once with `"key"`. Raise an unsubscribed signal; assert no effect and no exception.

**Acceptance Scenarios**:

1. **Given** a running graph and a listener on signal S, **When** the host raises S with payload P, **Then** the listener is invoked once with P.
2. **Given** no listener on signal S, **When** the host raises S, **Then** nothing happens and no exception is produced.
3. **Given** multiple listeners on S, **When** S is raised, **Then** every listener is invoked (broadcast), each receiving the same payload.
4. **Given** a signal raised without a payload, **When** delivered, **Then** listeners can observe a "no payload" state distinguishable from a scalar payload.

---

### User Story 2 - A graph pauses on a node awaiting a signal and resumes when it arrives (Priority: P1)

A graph author can mark a node so that, on entry, execution **holds** there — it does not advance — until a specified named signal is raised by the host; when that signal arrives, the graph advances normally. This is the push→pull bridge proper.

**Why this priority**: This is the headline capability — it turns an external event into graph progression, which is the entire point of the feature. US1 + US2 together are the MVP.

**Independent Test**: Build `start → [await "doorOpened"] → end`. Run; assert the runner reports it is waiting and has not ended. Raise `doorOpened`; assert it advances and ends.

**Acceptance Scenarios**:

1. **Given** execution at a node that awaits signal S, **When** no S has been raised, **Then** the runner reports it is waiting and does not advance past that node.
2. **Given** execution waiting on S, **When** the host raises S, **Then** the runner advances along the node's outgoing edge(s) using the same edge-selection rules as a normal advance.
3. **Given** a node that awaits S, **When** a *different* signal T is raised, **Then** the runner keeps waiting (T does not satisfy the wait).
4. **Given** a graph with no awaiting nodes, **When** it runs, **Then** its behavior is identical to today (no signals involved).

---

### User Story 3 - Graph logic reads a signal's payload (Priority: P2)

Conditions and actions can read the payload most recently delivered for a named signal, so a graph can branch or act on *what* happened (e.g. which item), not merely *that* it happened.

**Why this priority**: Adds expressive power (payload-aware branching), but US1 + US2 already deliver a working event bridge; this can land second without blocking the MVP.

**Independent Test**: `await "itemCollected"`; raise it with `"key"`; assert a condition that reads the payload sees `"key"` and selects the matching branch.

**Acceptance Scenarios**:

1. **Given** a signal raised with payload P, **When** a condition/action queries that signal's payload, **Then** it reads P.
2. **Given** a signal raised without a payload, **When** queried, **Then** logic can detect the absence and fall back safely.

---

### Edge Cases

- **Signal raised while nothing is awaiting it (early/late event)**: default behavior is **transient** — the signal is delivered to current subscribers only and is **not** remembered for a future await. (Latched/"sticky" delivery is out of scope for v1 — see Assumptions.)
- **Re-entrant raise** (a listener raises another signal while being notified): listeners are notified over a stable snapshot so subscription changes mid-notification do not corrupt iteration (mirrors existing `BaseContext` subscriber behavior).
- **Same signal raised multiple times while waiting**: the first matching raise satisfies the wait; further raises follow normal semantics.
- **Unsubscribe during notification**: honored on the next raise, not mid-iteration.
- **Null/empty signal name**: rejected with a `[GraphCore]` warning and treated as a safe no-op (invalid raise / invalid await), never an unhandled exception.
- **Step-back across an awaiting node**: if execution returns to a node that previously awaited a signal, the node **re-arms** its wait.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The runtime MUST let the host raise a signal identified by a non-empty name, optionally carrying a single payload value of a context-supported scalar type (bool/int/float/string), or no payload.
- **FR-002**: The runtime MUST support zero-to-many subscribers per signal name and deliver each raised signal to all current subscribers (broadcast).
- **FR-003**: Raising a signal with no subscribers MUST be a no-op — no error, no state change.
- **FR-004**: A graph author MUST be able to designate a node as awaiting a named signal, causing execution to hold at that node until the signal is raised.
- **FR-005**: When an awaited signal is raised, the runtime MUST advance from the awaiting node using the same edge-selection rules as a normal advance.
- **FR-006**: A signal whose name does not match the awaited name MUST NOT satisfy the wait.
- **FR-007**: Graph logic (conditions/actions) MUST be able to read the payload delivered with a signal and MUST be able to distinguish "no payload" from a scalar payload.
- **FR-008**: When a graph defines no awaiting nodes and the host raises no signals, runtime behavior MUST be identical to the pre-feature behavior (full back-compatibility).
- **FR-009**: The entire capability MUST be exercisable headlessly (editor closed, EditMode), with no `MonoBehaviour` and no `UnityEvent` dependency in the Runtime assembly.
- **FR-010**: Invalid use (null/empty signal name; awaiting an unnamed signal) MUST be handled gracefully with a `[GraphCore]`-prefixed warning and a safe no-op, never an unhandled exception.
- **FR-011**: The capability MUST be additive and append-only — no existing public signature, serialized field, or behavior is removed or changed (semver MINOR, graphcore 0.3.0 → 0.4.0).
- **FR-012**: Existing history/step-back and sub-graph mechanisms MUST continue to function unchanged; a node that returns to an awaiting state via step-back MUST re-arm its wait.
- **FR-013**: The capability MUST be verifiable in the `com.faolline.graphTest` sandbox, exercising raise, broadcast to N subscribers, await/resume, payload read, no-op-on-no-subscriber, and back-compatibility.

### Key Entities

- **Signal**: a named, optionally-payloaded event. Identity = its name; payload = at most one scalar (bool/int/float/string) or none.
- **Subscriber**: a party (graph logic or host-side) registered to be notified when a named signal is raised; many may listen to one name.
- **Awaiting node**: a node configured to hold execution until a named signal is raised, then advance.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The host can drive a graph from an external event end-to-end — a graph that holds on an await advances **solely** because the host raised the matching signal, with no other input.
- **SC-002**: 100% of the existing graphcore EditMode suite passes **unmodified** after the change (proof of non-breakage).
- **SC-003**: A graph that uses no signals exhibits zero observable difference from pre-feature behavior.
- **SC-004**: The full capability (raise, broadcast to N subscribers, await/resume, payload read, no-op on no subscriber, back-compat) is demonstrated by EditMode tests in graphTest, all green with the editor closed.
- **SC-005**: Raising a signal with no subscribers, and raising before or after any await, never throws.
- **SC-006**: A graph author can make a node wait for an event and a host integrator can resume it using only public API — no engine-internal code.

## Assumptions

- **Transient (edge-triggered) delivery in v1**: a raised signal reaches current subscribers and is not latched/remembered for a future await. Level-triggered / "sticky" delivery (for races such as "the event happened before the node was reached") is deferred to the Reactive engine (roadmap P3), which evaluates state rather than transient events.
- **Match on name only**: awaiting matches a signal by name; the payload is data for logic to read, not part of the match key.
- **One scalar payload slot**, typed to the existing context scalar set (bool/int/float/string), to stay consistent with `BaseContext` and avoid boxing arbitrary types in v1. Multi-value/struct payloads are out of scope.
- **Implementation choice deferred to plan.md (HOW, not WHAT)**: whether a signal is realized as a *notifying context write* (reusing `BaseContext.OnParameterChanged/OffParameterChanged`, ≈80% of the plumbing) or as a *separate event channel*. This spec is neutral; either approach can satisfy every requirement above.
- **Verification in `com.faolline.graphTest`** (throwaway sandbox), not a real downstream lib; no new runtime dependencies introduced.
- **Governance per constitution**: EditMode tests written failing-first (TDD); `[GraphCore]` log prefix; one class per file; XML docs on new public API; additions append-only and semver-MINOR.

## Out of Scope *(deferred to later roadmap primitives)*

- The Reactive and Flow engines (roadmap P3 / Flow), the generic threshold Join (P4), Time/Tick nodes (P5), context collections (P2), the one-shot "visited" mark (P6), and any resolution-ordering / priority policy.
- Latched/sticky signals; multi-value or struct payloads; signal scoping rules beyond "observable across the running instance".
