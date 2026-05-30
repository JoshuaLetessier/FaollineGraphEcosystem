# Feature Specification: GraphTest — Choices & ChooseById

**Feature Branch**: `007-graphtest-choices`

**Created**: 2026-05-29

**Status**: Draft

**Input**: User description: "étendre com.faolline.graphTest pour couvrir les choices et ChooseById de graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Author a Choice Node (Priority: P1)

A developer adds a `ChoiceNodeData` to the canvas, configures its choices (each with a label and optional condition), and connects each choice's output port to a target node. The choice node appears on the canvas with one output port per declared choice.

**Why this priority**: The choice node is the only branching mechanism in graphcore that requires explicit user selection at runtime. Without authoring support, all downstream libs that present options to the player are blocked.

**Independent Test**: Right-click the canvas → "Add Choice Node". In the inspector, add two choices: "Go left" and "Go right". Draw an edge from the "Go left" port to node A and from the "Go right" port to node B. Save and reload — both choices and their edges are still present.

**Acceptance Scenarios**:

1. **Given** an open graph editor window, **When** the developer adds a Choice node via the context menu, **Then** a choice node appears on the canvas with zero output ports (no choices yet).
2. **Given** a Choice node is selected in the inspector, **When** the developer adds a choice with label "Go left", **Then** a new output port labelled "Go left" appears on the node.
3. **Given** a Choice node with two choices, **When** the developer saves and reloads the graph, **Then** both choices and their connected edges are preserved exactly.
4. **Given** a choice with an output port, **When** the developer connects it to a target node, **Then** the edge is saved linking that choice's port to the target.
5. **Given** a choice entry in the inspector, **When** the developer removes it, **Then** the corresponding output port disappears and any connected edge is also removed.

---

### User Story 2 — Run to a Choice Node and Select a Choice (Priority: P1)

A developer clicks Run. Execution proceeds normally until the runner reaches a Choice node, at which point it pauses. The inspector panel shows the available choices. The developer selects one via the Choose button in the toolbar, and execution resumes on the selected branch.

**Why this priority**: This is the core verification scenario — without the ability to pause at a choice and resume on a specific branch, there is no way to confirm that `ChooseById` routes execution correctly.

**Independent Test**: Build a graph: Start → Choice (choices: "Left", "Right") → two Statement nodes (A, B) → End. Click Run — execution stops at Choice, console shows `[GraphTest] Waiting for choice at: graphcore/choice`. Click Choose → select "Left" → execution visits Statement A → End. Repeat and select "Right" → visits Statement B.

**Acceptance Scenarios**:

1. **Given** a graph where Run reaches a Choice node, **When** execution arrives at that node, **Then** execution pauses, the console logs `[GraphTest] Waiting for choice`, and the Choose button in the toolbar becomes active.
2. **Given** execution is paused at a Choice node, **When** the developer opens the inspector, **Then** the inspector shows the list of available choices with their labels.
3. **Given** the developer clicks Choose and selects "Left", **When** the choice is confirmed, **Then** `ChooseById` is called with the "Left" choice's ID, execution resumes, and the console logs the next visited node.
4. **Given** execution is paused at a Choice node, **When** the developer clicks Run again, **Then** the session resets and execution starts from the beginning.
5. **Given** execution has completed (State == Ended), **When** the developer clicks Choose, **Then** a console message states "No active choice — click Run first" and nothing happens.

---

### User Story 3 — Conditional Choices (Priority: P2)

A developer attaches a condition to one or more choices on a Choice node. At runtime, choices whose condition fails are greyed out in the inspector and cannot be selected. Only choices whose condition passes (or have no condition) are available.

**Why this priority**: Conditional choices are the branching mechanism for dynamic content — a dialogue option that only appears if the player has a certain item, for example. The test package must verify this works before any real lib depends on it.

**Independent Test**: Create a Choice node with two choices: "Open door" (condition: `TestBoolCondition` key="door_open" expected=true) and "Leave" (no condition). Run graph with `door_open = false` → inspector shows "Open door" as unavailable, only "Leave" can be selected. Run with `door_open = true` → both choices available.

**Acceptance Scenarios**:

1. **Given** a choice with a `TestAlwaysFalseCondition`, **When** execution reaches the Choice node, **Then** that choice does not appear in the selectable list.
2. **Given** a choice with a `TestAlwaysTrueCondition`, **When** execution reaches the Choice node, **Then** that choice appears in the selectable list.
3. **Given** all choices on a Choice node have failing conditions, **When** execution reaches that node, **Then** the runner fires `OnStuck`, the console logs `[GraphTest] Execution stopped: runner is stuck` and execution halts.
4. **Given** a choice whose condition passes and one whose condition fails, **When** the developer can only select the passing choice, **Then** selecting it correctly routes execution to its connected node.

---

### Edge Cases

- What if a Choice node has no choices at all? Execution logs `[GraphTest] Choice node has no choices — stuck` and halts (OnStuck fires).
- What if a choice's output port is not connected to any node? Selecting that choice causes OnStuck — the runner finds no valid edge.
- What if the developer connects the same target node to two different choice ports? Both choices route to the same node — valid, no error.
- What if GoBack is called while execution is paused at a choice? The runner steps back to the previous node, the "waiting for choice" state clears, and execution resumes from the restored node.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The context menu on the canvas MUST include an "Add Choice Node" entry that creates a `ChoiceNodeData` on the canvas.
- **FR-002**: The inspector panel for a selected Choice node MUST display the list of its choices, each showing a label and an optional condition field.
- **FR-003**: The inspector MUST provide an "Add Choice" button that appends a new `TestChoice` (with a configurable label) to the node and immediately creates a corresponding output port on the canvas view.
- **FR-004**: The inspector MUST provide a Remove button per choice that removes the choice and its output port from both the data and the canvas view.
- **FR-005**: Each output port on a Choice node MUST be named with the choice's label, and the underlying port name used for edge routing MUST match the choice's ID.
- **FR-006**: When execution reaches a `ChoiceNodeData`, the execution loop MUST pause and set a `_waitingForChoice` flag. The console MUST log `[GraphTest] Waiting for choice at node: {nodeId}`.
- **FR-007**: While waiting for a choice, the toolbar Choose button MUST become active and display the available (condition-passing) choices.
- **FR-008**: Selecting a choice via the Choose button MUST call `runner.ChooseById(choiceId)` and resume the execution loop until the next choice node or the end of the graph.
- **FR-009**: Choices whose condition fails at runtime MUST be excluded from the selectable list. A choice with no condition is always available.
- **FR-010**: If no choices pass their conditions, execution MUST halt with a `[GraphTest] Execution stopped: runner is stuck` warning (consistent with existing stuck behavior).
- **FR-011**: The Choose button MUST be inactive (disabled or no-op with message) when no session is active or when execution is not paused at a Choice node.
- **FR-012**: GoBack while paused at a Choice node MUST clear the `_waitingForChoice` flag and restore the runner to the previous node.

### Key Entities

- **TestChoice**: Concrete `BaseChoice` subclass with a `Label` string field, used in the test package to give choices human-readable names.
- **ChoiceNodeView**: Canvas view for `ChoiceNodeData` — renders one input port and dynamically creates/removes output ports as choices are added or removed.
- **ChoiceNodeInspectorSection**: The inspector section rendered when a Choice node is selected — lists choices, exposes Add/Remove, label editing, and condition assignment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Choice node with two choices can be authored, saved, and reloaded with both choices and their connected edges intact — 100% fidelity.
- **SC-002**: Run on a Start → Choice → two branches → End graph correctly pauses at the Choice node and resumes on the selected branch for both choices — verified in two separate runs.
- **SC-003**: A choice with `TestAlwaysFalseCondition` never appears in the selectable list when execution is paused at its node.
- **SC-004**: GoBack from a completed run with a choice in history correctly restores the pre-choice state, allowing a different choice to be made on the next Continue.
- **SC-005**: The Choose button is inactive before any Run and after a completed run with no pending choice — clicking it produces a clear console message, not an exception.

## Assumptions

- `ChoiceNodeData` and `BaseChoice` are already implemented in `com.faolline.graphcore` and require no changes.
- `ChooseById` on `BaseRunner` selects the outgoing edge whose `PortName` matches the provided ID — the choice's `Id` GUID is used as the port name when edges are drawn from a choice's output port.
- The "waiting for choice" state is represented by a boolean flag on `TestGraphEditorWindow` — execution resumes in the same synchronous loop when `ChooseById` is called.
- `TestChoice` is a minimal subclass — only a `Label` string. More complex choice types (localized text, cost, etc.) belong in downstream libs.
- Adding/removing choices from the inspector requires a live canvas refresh — dynamically updating the port count without reloading the whole graph.
- Copy/paste and undo/redo of choice nodes are out of scope for this feature.
