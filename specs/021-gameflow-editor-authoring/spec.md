# Feature Specification: gameflow editor authoring (slice 2)

**Feature Branch**: `021-gameflow-editor-authoring`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "The editor authoring layer for com.faolline.graphgameflow — a creatable gameflow graph asset, a visual graph editor window with node views and an inspector, a creatable LoadSceneAction, and a sample builder — so a gameflow can be designed entirely in the editor (no code) and assigned to the GraphFlowDriver. Slice 1 delivered the runtime; the user cannot currently create a gameflow graph at all."

## Overview

Slice 1 (spec 020) shipped the gameflow **runtime** host bridge but deferred all editor authoring, so a
gameflow graph can today only be built in **code**. A user reported they cannot create one at all — not via
**Assets ▸ Create**, not even a sample. Every other usable graph package in the ecosystem
(`starterGraph`, `graphdialoguesystem`) ships an **Editor** with a creatable graph asset, a visual editor
window, node views, an inspector, and a sample builder. This slice gives gameflow the same, turning it from
a code-only runtime into a **designable tool**. It also fixes a concrete slice-1 oversight: `LoadSceneAction`
has no **Create** menu entry, unlike every other action in the ecosystem.

This is additive and editor-focused: it reuses graphcore's editor infrastructure and adds only the
gameflow-specific window / views / inspector / sample, plus a trivial creatable graph asset type. The slice-1
runtime is unchanged.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Game designer** — creates a gameflow graph asset, authors its nodes/edges visually, and configures each
  node (scene loads, signal waits) in the inspector — without writing code.
- **Developer** — assigns the authored graph to a `GraphFlowDriver` and presses Play; also uses the one-click
  sample as a starting point.

### User Story 1 - Create and author a gameflow graph visually (Priority: P1) 🎯 MVP

A designer creates a gameflow graph from **Assets ▸ Create**, double-clicks it to open the gameflow editor
window, and authors the flow: add Start / Statement / Choice / SubGraph / End nodes, connect them with edges,
move and delete them — exactly as the StarterGraph editor works.

**Why this priority**: Without a creatable asset and a window to author it, gameflow cannot be used by anyone
who isn't writing C#. This is the gap the user hit.

**Independent Test**: from a clean editor, **Create ▸ GraphGameFlow ▸ Game Flow Graph** produces a graph
asset; double-clicking it opens the gameflow window; the designer adds nodes and connects edges and the graph
asset records them.

**Acceptance Scenarios**:

1. **Given** a clean project, **When** the designer uses Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow Graph,
   **Then** a gameflow graph asset is created.
2. **Given** a gameflow graph asset, **When** the designer double-clicks it, **Then** the gameflow editor
   window opens showing that graph.
3. **Given** the open window, **When** the designer adds the universal node types (Start, Statement, Choice,
   SubGraph, End) and connects edges, **Then** the nodes and edges are saved in the asset.
4. **Given** the authored graph, **When** it is assigned to a `GraphFlowDriver` and Play is pressed, **Then**
   it runs (a `GraphFlowGraph` is a graph the driver accepts unchanged).

### User Story 2 - Configure scene loads and signal waits in the inspector (Priority: P1)

Selecting a node shows an inspector where the designer configures, **without code**, the things that make a
gameflow do something: attach/remove enter- and exit-actions (e.g. drop in a **Load Scene** action), edit
entry conditions, set an **await-signal** name (to make the node wait for a cue), set a **wait duration**, and
mark a checkpoint. The Load Scene action itself is creatable from **Assets ▸ Create**.

**Why this priority**: Authoring the graph shape is meaningless if the designer can't make a node load a scene
or wait for a signal. This inspector is what makes the gameflow's actual behavior designable.

**Independent Test**: with a node selected, the inspector attaches a Load Scene action to its enter list and
sets an await-signal name; the node data reflects both; a Load Scene action asset can be created from the
Create menu and dropped in.

**Acceptance Scenarios**:

1. **Given** a selected node, **When** the designer attaches a Load Scene action to its enter (or exit) list,
   **Then** the node carries that action.
2. **Given** a selected node, **When** the designer sets an await-signal name, **Then** the node becomes an
   await-signal node (the runtime parks on it until that signal).
3. **Given** a selected node, **When** the designer sets a wait duration or toggles checkpoint, **Then** the
   node data reflects it.
4. **Given** a clean project, **When** the designer uses Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load
   Scene, **Then** a Load Scene action asset is created, ready to drop into a node's action list.

### User Story 3 - One-click runnable sample (Priority: P2)

A designer runs a menu command that generates the **reference scene-flow** as a ready-to-run asset:
start → [enter: Load Scene A] → await "advance" → [enter: Load Scene B] → end, with the Load Scene actions
created and attached. Assigning it to a driver and pressing Play demonstrates the whole flow.

**Why this priority**: A runnable example is the fastest way to understand and validate the package; the other
graph packages all ship one. It builds on US1/US2 (it produces the same asset shapes).

**Independent Test**: running the sample menu produces a gameflow graph asset matching the reference flow
(the five nodes, the four edges, the await-signal node, the two Load Scene actions); driving that asset with a
`GraphFlowDriver` walks A → await → B → end.

**Acceptance Scenarios**:

1. **Given** a clean project, **When** the designer runs the GraphGameFlow sample menu, **Then** a gameflow
   graph asset is created with the reference flow's nodes, edges, await node, and two attached Load Scene
   actions.
2. **Given** the generated sample, **When** it is driven by a `GraphFlowDriver`, **Then** it loads scene A,
   parks on the await node, and on the "advance" signal loads scene B and ends.

### Edge Cases

- **Opening a non-gameflow graph (a bare `BaseGraph`) in the gameflow window** → handled the same way the
  other package windows handle a foreign graph (the window targets its own graph type; no crash).
- **A node with no node view registered** → falls back to the base node view (no crash), consistent with the
  graphcore editor infrastructure.
- **Re-running the sample menu** → produces a fresh asset (e.g. a uniquely-named asset) rather than silently
  overwriting, so an existing authored sample is never clobbered.
- **Removing an action/condition from a node in the inspector** → the node data drops it; the asset stays
  valid.
- **Validating the graph** (missing Start, dangling edges) → surfaced via graphcore's existing
  `GraphValidator`, not re-implemented.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The package MUST provide a creatable gameflow graph asset type (a `BaseGraph`) available under
  **Assets ▸ Create ▸ GraphGameFlow**, accepted unchanged by the slice-1 `GraphFlowDriver`.
- **FR-002**: The package MUST provide a visual editor window that opens when a gameflow graph asset is
  double-clicked and lets the author add, connect, move, and delete the universal node types (Start,
  Statement, Choice, SubGraph, End) with edges, persisting them to the asset.
- **FR-003**: The package MUST provide an inspector for a selected node that lets the author, without code:
  attach/remove enter-actions and exit-actions, edit entry conditions, set the await-signal name, set the
  wait duration, and toggle checkpoint.
- **FR-004**: `LoadSceneAction` MUST be creatable from **Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load
  Scene** and droppable into a node's action list in the inspector.
- **FR-005**: The package MUST provide a menu command that generates the reference scene-flow as a runnable
  asset (start → load A → await "advance" → load B → end, with the two Load Scene actions attached).
- **FR-006**: The editor MUST reuse graphcore's editor infrastructure (graph view, node views base, inspector
  base, edge view, validation, copy/paste-with-new-GUIDs, groups) and only add the gameflow-specific
  subclasses + node views + sample — it MUST NOT reimplement what graphcore provides.
- **FR-007**: graphcore and graphstandard MUST be unchanged. The slice-1 gameflow runtime MUST be unchanged
  and its existing 654 EditMode + 8 PlayMode tests MUST stay green. The package MUST bump `0.1.0 → 0.2.0`
  (semver MINOR, additive).
- **FR-008**: The generated sample and the authored graph MUST run identically under the driver (the editor
  produces the same data the code path does); the await-signal node and the Load Scene actions MUST behave as
  in slice 1.
- **FR-009**: Dev standards: `[GraphGameFlow]` log prefix; one class per file; node-view styling via USS (no
  inline CSS); XML docs on public API; README + CHANGELOG updated.

### Key Entities

- **GameFlowGraph**: the creatable gameflow graph asset (a `BaseGraph`); what the window targets and the
  driver runs.
- **GameFlow editor window**: the visual canvas for authoring a gameflow graph.
- **Node views**: the per-node-type visuals for the universal node set (Start, Statement, Choice, SubGraph,
  End) + an edge view.
- **Node inspector**: the panel that edits a selected node's actions, conditions, await-signal, wait
  duration, and checkpoint.
- **LoadSceneAction (creatable)**: the slice-1 scene action, now with a Create-menu entry.
- **GameFlow sample builder**: the menu command that generates the runnable reference flow.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can create a gameflow graph asset from the Create menu without writing any code.
- **SC-002**: Double-clicking a gameflow graph asset opens the gameflow editor window showing that graph.
- **SC-003**: A user can author a complete flow (nodes + edges) in the window and the asset records it.
- **SC-004**: A user can attach a Load Scene action and set an await-signal name on a node entirely in the
  inspector, and the node data reflects both.
- **SC-005**: A Load Scene action asset can be created from the Create menu.
- **SC-006**: The sample menu generates a gameflow graph that, driven by a `GraphFlowDriver`, walks
  A → await → B → end (verified headless with a recording loader).
- **SC-007**: graphcore/graphstandard untouched; the slice-1 654 EditMode + 8 PlayMode tests stay green;
  gameflow ships `0.2.0` with README + CHANGELOG updated.

## Assumptions

- **Mirror starterGraph**: the window / view / node-view / inspector / edge-view / sample-builder structure
  copies the `com.faolline.starterGraph` editor (the project's "base to duplicate"), adapted to gameflow's
  Create-menu naming and the Load Scene affordance. The universal node-type set is the same.
- **Testable vs. visual**: EditMode tests cover the data/asset surface — the graph carries the Create
  attribute, `LoadSceneAction` carries the Create attribute, and the sample builder produces the exact
  reference structure and runs under the driver. Pure pointer interaction (dragging nodes, drawing edges by
  hand) is validated by the sample opening in the window, not unit-tested — mirroring how the other package
  editors are validated.
- **No runtime semantics added**: the only runtime addition is the trivial `GameFlowGraph : BaseGraph`
  subclass and the `LoadSceneAction` Create attribute; all behavior remains slice-1's.
- **Scene change stays an action**: the editor exposes Load Scene as an action in the node inspector, never as
  a dedicated node type (the locked slice-1 decision).
- **Driver inspector**: the default Unity component inspector for `GraphFlowDriver` is sufficient for
  assigning the graph and toggling AutoAdvance; a custom component inspector is out of scope.

## Out of Scope *(deferred)*

- A gameflow-specific node TYPE beyond the universal set (scene change stays an action, not a node).
- Custom per-node visual styling beyond what mirrors starterGraph.
- Advanced editor UX (node search, minimap, auto-layout) beyond what graphcore already provides.
- Reactive / Flow authoring (those engines' editors are separate future work).
- Any runtime behavior change beyond the trivial graph subclass + the action attribute.
- A custom component inspector for `GraphFlowDriver`.
