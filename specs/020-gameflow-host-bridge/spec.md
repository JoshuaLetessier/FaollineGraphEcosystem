# Feature Specification: gameflow host bridge + Linear scene-flow (vertical slice 1)

**Feature Branch**: `020-gameflow-host-bridge`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "The first vertical slice of a fresh com.faolline.graphgameflow package — the canonical Unity HOST BRIDGE that runs the headless graphcore foundation inside a real Unity scene, proven in PlayMode. A MonoBehaviour driver references a graph asset, owns the shared context, boots and drives the Linear runner, forwards per-frame deltaTime, lets the scene inject signals, surfaces runner events, and the reference flow loads scene A → waits on a signal → transitions to scene B over one shared context."

## Overview

Everything below the orchestrator (graphcore, graphstandard) is **headless** and has only ever run in
EditMode unit tests. This feature adds the first piece of a **fresh `com.faolline.graphgameflow`** package:
the **host bridge** — the adapter that lets a Unity scene actually *run* a graph. It is deliberately the one
layer where Unity vocabulary (a scene component, scene loading, the per-frame update, a graph asset) is
allowed to live; the libraries beneath it stay universal and headless.

The slice is proven end-to-end by a **scene-flow**: a single graph that, driven only by the host bridge,
enters a node that loads **scene A**, parks on a node that waits for an **advance** signal, and on that
signal transitions to **scene B** — all sharing one context. This converts the "open seam" between the
headless foundation and the Unity runtime into a **proven seam** and gives a package skeleton to grow.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Game integrator** — drops the driver component into a scene, assigns a graph asset, presses Play, and
  expects the flow to run and to be able to react to it from their own scripts.
- **Flow author** — designs the graph (enter-a-scene nodes, an await-signal node) that the driver runs.
- **(transitive) future gameflow slices** — Reactive progression and Flow abilities will be hosted by this
  same driver/context later.

### User Story 1 - Boot and drive a flow from a scene component (Priority: P1) 🎯 MVP

A game integrator adds the driver component to a GameObject, assigns a graph asset, and presses Play. The
driver boots the Linear runner over a shared context, advances through the graph, forwards the per-frame
time tick so time/await nodes can resolve, and surfaces the runner's lifecycle events so the integrator's
own scripts can react (node entered, node completed, flow ended, flow stuck).

**Why this priority**: This is the bridge itself — the first time the headless runtime executes inside a
live scene. Nothing else in gameflow can exist without it.

**Independent Test**: a graph of plain statement nodes (start → … → end) assigned to the driver runs to
completion in PlayMode, raising the lifecycle events in order, with no manual wiring beyond pressing Play.

**Acceptance Scenarios**:

1. **Given** a driver with a valid graph asset assigned, **When** the scene enters Play, **Then** the runner
   boots and the start node is entered (a node-entered event is raised).
2. **Given** a running flow set to auto-advance, **When** a node completes, **Then** the driver advances to
   the next node until an End node raises the flow-ended event.
3. **Given** a running flow, **When** a frame elapses, **Then** the driver forwards that frame's elapsed time
   to the runner (so a time-wait node counts down and resolves).
4. **Given** an integrator script subscribed to the driver's events, **When** the flow runs, **Then** the
   script receives node-entered / node-completed / ended events for the corresponding nodes.

### User Story 2 - A scene transition is an action attachable to any node (Priority: P1)

Loading a Unity scene is an **action like any other** — not a dedicated node type. A flow author attaches a
"load scene" action to **any** node's enter or exit action list (a statement, a choice, a subgraph node, the
start or end node — whatever the flow needs), exactly as they would attach any other graphcore action. When
the driver runs that node and reaches the action, the named scene is loaded (single or additive). This is
the first concrete gameflow "standard action" and the reason the bridge must live in the Unity-aware layer.

**Why this priority**: Scene transition is the defining job of a game flow; without it the bridge would only
mutate data, never change what the player sees. It is half of the reference scene-flow. Modeling it as an
action (not a node) keeps it composable — it can fire on entering *or* exiting any node type, alongside
other actions and conditions.

**Independent Test**: a one-node graph whose node carries a "load scene B" enter-action; when the driver
enters the node, scene B is loaded (observable via the active/loaded scene). The same action attached to a
choice node's exit list, or a subgraph node's enter list, behaves identically.

**Acceptance Scenarios**:

1. **Given** a "load scene" action (single mode) on a node's enter list, **When** the driver enters that
   node, **Then** that scene becomes the active scene.
2. **Given** the same action on a node's exit list, **When** the driver advances out of that node, **Then**
   the scene is loaded on exit — proving it is not tied to node entry or to a special node type.
3. **Given** a "load scene" action configured additive, **When** it runs, **Then** that scene is loaded in
   addition to the current one.
4. **Given** a "load scene" action attached to different node types (statement, choice, subgraph), **When**
   each runs, **Then** the scene loads identically regardless of host node type.
5. **Given** a "load scene" action whose scene does not exist / is not in build settings, **When** it runs,
   **Then** a `[GraphGameFlow]` error is logged and the flow does not crash.

### User Story 3 - Resume an awaiting flow by raising a signal from the scene (Priority: P2)

A flow parks on an **await-signal** node. Scene code (a button, a trigger volume, an input handler) calls the
driver to **raise a named signal**; the flow resumes past the awaiting node. This closes the loop between
in-scene gameplay and flow progression.

**Why this priority**: The reference scene-flow waits between scene A and scene B for an external cue; this is
how the player (or scene logic) drives the flow forward. It builds on US1's running driver.

**Independent Test**: a graph parked on an await-signal node; calling the driver's raise-signal with the
matching name advances the flow; calling it with a non-matching name does nothing.

**Acceptance Scenarios**:

1. **Given** a flow parked on an await-signal node, **When** the scene raises the matching signal, **Then**
   the flow resumes and advances past the awaiting node.
2. **Given** a flow parked on an await-signal node, **When** the scene raises a different signal, **Then** the
   flow stays parked.
3. **Given** a flow NOT currently awaiting a signal, **When** the scene raises a signal, **Then** nothing
   breaks (the signal is a no-op for flow progression).
4. **Given** the full reference flow (enter scene A → await "advance" → enter scene B), **When** Play starts
   and then "advance" is raised, **Then** scene A loads first, the flow waits, and on the signal scene B
   loads — all over one shared context.

### Edge Cases

- **No graph asset assigned** → the driver logs a `[GraphGameFlow]` warning on Play and stays inert (no
  crash, no NullReference).
- **Graph has no valid start node** → the driver logs a warning and does not boot.
- **Scene name missing / not in build settings** (US2) → a logged error, the flow continues without the
  scene change.
- **Signal raised before the driver has booted, or after the flow has ended** → no-op, no exception.
- **Per-frame tick with zero or negative elapsed time** → ignored (no negative countdown).
- **Driver component disabled or destroyed mid-flow** → the runner stops being pumped; no exception is thrown
  from the dangling subscription.
- **Manual-advance mode** → when auto-advance is off, the flow only advances when the integrator calls the
  driver's advance method (so a node can stay on screen until the player acts).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The package MUST provide a scene component (driver) that references a graph asset and owns a
  single shared context for the run.
- **FR-002**: On entering Play, the driver MUST boot the Linear runner over the assigned graph and shared
  context and enter the start node, OR log a `[GraphGameFlow]` warning and stay inert when no valid graph /
  start node is available.
- **FR-003**: The driver MUST forward each frame's elapsed time to the runner's time tick so time-based and
  await nodes can resolve; zero/negative elapsed time MUST be ignored.
- **FR-004**: The driver MUST support both **auto-advance** (advance automatically when a node completes) and
  **manual-advance** (advance only when the integrator requests it), selectable on the component.
- **FR-005**: The driver MUST re-expose the runner's lifecycle events (node entered, node completed, flow
  ended, flow stuck) as subscribable hooks for in-scene scripts.
- **FR-006**: The driver MUST expose a method to raise a named signal (with an optional payload) into the
  running flow; raising a signal MUST resume a flow parked on a matching await-signal node and MUST be a
  safe no-op otherwise.
- **FR-007**: The package MUST provide a scene-load **action** (a graphcore action, NOT a dedicated node
  type) that loads a Unity scene by name in single or additive mode when it runs. It MUST be attachable to
  any node's enter OR exit action list, regardless of node type (statement, choice, subgraph, start, end),
  and behave identically in all cases. A missing/invalid scene MUST log a `[GraphGameFlow]` error without
  crashing the flow.
- **FR-008**: The reference scene-flow (enter scene A → await "advance" → enter scene B, one shared context)
  MUST run end-to-end under the driver and be verifiable in PlayMode.
- **FR-009**: The lower libraries (graphcore, graphstandard) MUST be unchanged; this is a new additive
  package `com.faolline.graphgameflow` at `0.1.0`, depending on `com.faolline.graphcore` `0.6.0` (pinned,
  not a `0.0.0` placeholder). The existing 634-test EditMode suite MUST stay green.
- **FR-010**: Unity-specific concerns (scene component, scene loading, the frame tick, the graph asset)
  MUST be confined to this package; they MUST NOT leak into graphcore/graphstandard.
- **FR-011**: The package MUST ship a README and CHANGELOG from its first version, and public API MUST carry
  XML docs and the `[GraphGameFlow]` log prefix on misuse.

### Key Entities

- **Flow driver**: the scene component that binds a graph asset + shared context to the Unity runtime —
  boots the runner, pumps the frame tick, forwards signals, and surfaces lifecycle events.
- **Scene-load action**: a graphcore action (not a node type) carrying a scene reference and a load mode;
  attachable to any node's enter or exit action list and loads the scene when it runs.
- **Shared context**: the single typed blackboard carried across the whole flow (and, in later slices, shared
  with Reactive/Flow subsystems).
- **Advance signal**: the named cue a scene raises to resume an awaiting flow.
- **Reference scene-flow graph**: start → a node carrying a load-scene-A action → an await-"advance" node →
  a node carrying a load-scene-B action → end. The "scene" nodes are ordinary nodes; only their attached
  action is scene-specific.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An integrator can run a flow graph in a scene by only adding the driver component, assigning a
  graph asset, and pressing Play — no code required to boot it.
- **SC-002**: A flow of statement nodes runs start-to-end under the driver in PlayMode, raising the lifecycle
  events in the correct order.
- **SC-003**: Entering a scene-load node changes the loaded/active Unity scene (single and additive both
  observable).
- **SC-004**: A flow parked on an await-signal node resumes within one frame of the matching signal being
  raised, and ignores non-matching signals.
- **SC-005**: The full reference flow walks scene A → (wait) → scene B over one shared context, driven only by
  the component + one signal call, verified by a PlayMode test.
- **SC-006**: Every listed edge case (no graph, missing scene, stray signal, zero/negative tick, destroyed
  driver) is handled with a log + graceful continuation, never an unhandled exception.
- **SC-007**: graphcore/graphstandard are untouched; the existing 634-test EditMode suite stays green;
  `com.faolline.graphgameflow` ships at `0.1.0` with README + CHANGELOG.

## Assumptions

- **PlayMode for the genuine scene path, EditMode for wiring**: scene loading and the frame pump are
  PlayMode-bound, so the reference flow is a PlayMode test. Driver wiring (boot, tick forwarding, signal
  forwarding, event re-exposure) is tested in EditMode against a **stubbed scene-load seam** so most coverage
  stays fast and deterministic; PlayMode is reserved for proving the real scene-load path.
- **Linear engine only** for this slice: the driver hosts the graphcore `BaseRunner`. Reactive/Flow hosting
  is a later slice and is why the context is shared rather than runner-private.
- **Single transition is enough to prove the seam**: scene unloading and async-load orchestration beyond the
  one A→B transition are deferred.
- **Scenes are addressed by name** and assumed present in build settings (or loaded via a test seam); build
  settings management is the integrator's responsibility.
- **One driver per flow**: multiple concurrent drivers / nested flow drivers are out of scope for this slice.
- **graphcore already provides** the await-signal node behavior, the time tick, and the action model used
  here (shipped in graphcore 0.6.0); this feature only binds them to Unity, it does not add runtime
  semantics to the lower libs.

## Out of Scope *(deferred to later slices)*

- Reactive progression integration and Flow ability execution hosted by the driver.
- Save/load persistence (the `com.faolline.savesystem.core` integration).
- The resolution / priority policy (axis A).
- Editor authoring tooling or custom inspectors for the driver/graph.
- Multiple driver variants; nested/concurrent flow drivers.
- Scene unloading and async-load orchestration beyond the single A→B transition.
- Dialogue / localization wiring.
