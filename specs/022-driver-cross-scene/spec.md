# Feature Specification: gameflow driver cross-scene hardening (slice 3)

**Feature Branch**: `022-driver-cross-scene`

**Created**: 2026-06-10

**Status**: Draft

**Input**: User description: "Harden the GraphFlowDriver for the real multi-scene case dogfooding exposed — a single driver running one graph that spans scenes via single-mode scene loads currently destroys itself on the first load, so the documented flow can't run — plus re-expose the time-wait event, add a boot-on-start toggle and a waiting-state query, and add the REAL cross-scene PlayMode test that was missing (the stub loader masked scene destruction)."

## Overview

A consumer built an escape-room as **one graph spanning three scenes** (MainMenu → Room → Win) and hit a wall:
a single `GraphFlowDriver` that loads the next scene in **single mode** is **destroyed by that very load**
(its GameObject tears down with the old scene), so nothing can advance the parked flow afterward — the
documented reference flow literally cannot run. They worked around it with a hand-written persistent
bootstrap. Three smaller ergonomics gaps were each worked around too.

Crucially, this whole class of bug shipped with **659 EditMode + 8 PlayMode tests green**, because the
test seam — an injected stub scene-loader — **records** scene loads but never **tears a scene down**. The
real failure lives precisely where the stub hid it. This slice fixes the driver and, just as importantly,
adds the **genuine cross-scene test** that should have caught it.

This is additive hardening of `com.faolline.graphgameflow` (**0.2.0 → 0.3.0**); the substrate
(`graphcore`/`graphstandard`) and the slice-1/2 runtime API are untouched.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Game integrator** — drives a multi-scene game from one flow graph and one driver; reacts to the flow from
  scene scripts.
- **Library maintainer** — must have a real regression test so a cross-scene flow can never silently break
  again.

### User Story 1 - A single driver runs a flow across scene loads (Priority: P1) 🎯 MVP

An integrator puts one driver on a GameObject, assigns a graph that loads scene A, waits for a cue, then loads
scene B (single mode), and enables a "persist across scenes" option. Pressing Play, the driver survives each
scene load and keeps running the flow to the end; scene scripts in the loaded scenes can still reach the
driver and raise signals.

**Why this priority**: The documented multi-scene flow is impossible without this. It is the headline gap.

**Independent Test**: a flow `load A → await → load B → end` over one persistent driver: a real run loads A,
waits, then on the signal loads B and reaches the end — the driver is alive throughout.

**Acceptance Scenarios**:

1. **Given** a driver with "persist across scenes" enabled, **When** its flow performs a single-mode scene
   load, **Then** the driver (and its in-progress flow) survives the load.
2. **Given** the persistent driver after a scene load, **When** a script in the newly-loaded scene raises the
   awaited signal, **Then** the flow advances (it was never destroyed).
3. **Given** "persist across scenes" disabled (the default), **When** the driver's GameObject is loaded with a
   new single-mode scene, **Then** it behaves as before (no persistence) — existing single-scene uses are
   unchanged.
4. **Given** a persistent driver has booted, **When** a scene script needs the driver, **Then** it can reach
   it through a documented accessor without writing its own singleton.

### User Story 2 - React to timed waits from the driver (Priority: P2)

A scene reacts when the flow enters or leaves a **timed** node (e.g. a 2-second intro), using the driver's
public events — symmetric with how it already reacts to signal waits.

**Why this priority**: Today the driver exposes the signal-wait event but not the time-wait one, so timed
beats are invisible to scene code (the consumer had to infer "intro done" from the *next* signal wait).

**Independent Test**: a flow with a timed node; subscribing to the driver's time-wait event fires when the
node is entered.

**Acceptance Scenarios**:

1. **Given** a flow entering a timed node, **When** the node is entered, **Then** the driver raises a
   time-wait event carrying the node and the duration.
2. **Given** a subscriber to that event, **When** the timed node resolves and the flow moves on, **Then** the
   subscriber received the event for that node.

### User Story 3 - Boot control and waiting-state query (Priority: P2)

An integrator (or a test) can stop the driver from auto-booting on Play so they can configure it first and
boot explicitly; and a scene that subscribes **late** can read the flow's current parked state instead of
missing an event that already fired during the scene load.

**Why this priority**: Both were worked around by the consumer (a double-boot warning during tests; reading
the runner's internals to recover a missed wait). Small, but they remove real friction.

**Independent Test**: with auto-boot disabled, `Start` does not boot and an explicit boot does not warn;
while parked on a signal-wait, the driver reports the awaited signal name.

**Acceptance Scenarios**:

1. **Given** auto-boot is disabled, **When** the scene starts, **Then** the driver does not boot until asked,
   and an explicit boot afterward does not log an "already running" warning.
2. **Given** auto-boot is enabled (the default), **When** the scene starts, **Then** the driver boots as
   before.
3. **Given** the flow is parked awaiting a signal, **When** a script queries the driver, **Then** it reports
   the current awaited signal name (and that it is waiting) without reaching into the runner's current node.
4. **Given** the flow is not awaiting a signal, **When** a script queries, **Then** the driver reports it is
   not waiting (empty/false).

### Edge Cases

- **Two persistent drivers** (e.g. each scene embeds one) → the documented accessor reflects a single active
  driver; the slice does not silently merge them, but the cross-scene pattern is documented as "one
  persistent driver", and a second persistent driver booting is handled without crashing.
- **Persist disabled + a single-mode load that destroys the driver** → unchanged legacy behavior (the driver
  dies with its scene); this is the existing single-scene use and must keep working.
- **Querying waiting-state before boot / after end** → reports "not waiting", no exception.
- **Subscribing to the time-wait event but the flow has no timed node** → simply never fires; no error.
- **Auto-boot disabled and the integrator forgets to boot** → the flow never starts (no crash); same as a
  driver with no graph.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The driver MUST offer an opt-in "persist across scenes" option (default OFF) that keeps the
  driver and its in-progress flow alive across single-mode scene loads.
- **FR-002**: With persistence enabled, a flow that spans scenes via single-mode loads MUST run to completion
  under one driver (the driver is not destroyed by its own scene loads).
- **FR-003**: With persistence disabled (default), the driver's lifetime MUST be unchanged from slice 1/2
  (it dies with its scene) — existing single-scene uses MUST be unaffected.
- **FR-004**: The library MUST provide a documented way for a scene script to reach the active persistent
  driver without writing its own singleton.
- **FR-005**: The driver MUST re-expose a time-wait event (node + duration), symmetric with the existing
  signal-wait event, so scene code can react to timed nodes through the driver's public API.
- **FR-006**: The driver MUST offer a "boot on start" toggle (default ON) so auto-boot on Play can be
  disabled for explicit/configured boot (e.g. tests, runtime setup) without an "already running" warning.
- **FR-007**: The driver MUST expose the current waiting state (the awaited signal name, and whether it is
  waiting for a signal) so a late-subscribing scene can recover a wait that fired during a scene load,
  without reading the runner's internals.
- **FR-008**: A **real** cross-scene test MUST exist that actually performs single-mode scene loads (not a
  recording stub) and proves a persistent driver plus its in-progress flow survive the loads and reach the
  end. This regression test is mandatory — its absence is why the bug shipped.
- **FR-009**: graphcore and graphstandard MUST be unchanged. The slice-1/2 driver public API MUST stay
  append-only and source-compatible (new fields/event/read-only members only). The existing 659 EditMode +
  8 PlayMode tests MUST stay green. The package MUST bump `0.2.0 → 0.3.0`.
- **FR-010**: Documentation MUST prominently describe the cross-scene pattern (one persistent driver runs one
  graph that spans scenes; that is why persistence is needed) and note that larger games decompose into
  SubGraphs (a room/dialogue = a SubGraph node), which the substrate already supports.

### Key Entities

- **Persist-across-scenes option**: the driver flag that makes the driver outlive scene loads.
- **Active-driver accessor**: the documented entry point a scene script uses to reach the persistent driver.
- **Time-wait event**: the driver event raised when the flow enters a timed node (node + duration).
- **Boot-on-start toggle**: controls whether the driver auto-boots on Play.
- **Waiting-state query**: the driver's report of the currently awaited signal (and whether it is waiting).
- **Cross-scene regression test**: a real PlayMode test that loads scenes for real and proves survival.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An integrator can run a multi-scene flow from one driver by enabling one option — no hand-written
  persistence bootstrap required.
- **SC-002**: A real cross-scene run (load A → await → load B → end, single mode) reaches the end with the
  driver alive throughout, proven by an automated test that actually loads the scenes.
- **SC-003**: Existing single-scene uses behave exactly as before when persistence is left off.
- **SC-004**: Scene code can react to a timed node via the driver's public events.
- **SC-005**: Auto-boot can be disabled so an explicit boot does not warn; the driver reports its current
  awaited signal while parked.
- **SC-006**: graphcore/graphstandard untouched; the prior 659 EditMode + 8 PlayMode tests stay green;
  gameflow ships 0.3.0 with README + CHANGELOG updated.

## Assumptions

- **Option A (driver-level persist flag), not a packaged bootstrap component.** The chosen shape is a flag on
  the driver (+ an optional static active-driver accessor), not a shipped `GameFlowBootstrap`. Games keep
  their own bootstrap for app-level persistence (e.g. a save system); a driver placed on such a bootstrap
  already persists, so the flag is for the standalone case.
- **Default OFF for persistence**, because a driver may legitimately be single-scene and `DontDestroyOnLoad`
  is surprising as a default; the documentation carries the cross-scene guidance.
- **The single active-driver accessor assumes one driver.** Multi-driver games route through their own
  bootstrap; the accessor is a single-driver convenience.
- **The real cross-scene test uses committed minimal test scenes registered in Build Settings at edit time**
  (creating scenes during play mode is disallowed; registering already-existing committed scenes into the
  build settings from a `UNITY_EDITOR`-guarded setup is fine), then loads them for real.
- **No new runtime semantics in the substrate** — the runner already has the signal/time/await behavior; this
  slice only binds it more completely and persists the host.

## Out of Scope *(deferred)*

- A packaged `GameFlowBootstrap` component (option B).
- Reactive / Flow hosting by the driver; save/load.
- The graphcore README catch-up (its README is stale at 0.2.0 vs. 0.6.0 code — undocumented signals / time /
  await / collections / scoped contexts). That is a real but **separate** doc fix on a different package, done
  as a companion commit, not part of this gameflow slice's code.
