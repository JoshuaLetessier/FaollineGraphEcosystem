# Feature Specification: GraphTest — Conditions, Actions & Checkpoints

**Feature Branch**: `006-graphtest-conditions-actions`

**Created**: 2026-05-29

**Status**: Draft

**Input**: User description: "étendre com.faolline.graphTest pour couvrir les conditions, actions et checkpoints de graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Declare Graph Parameters (Priority: P1)

A developer declares boolean parameters on a test graph using a dedicated panel in the editor window. These parameters become available as keys for conditions and actions to read and write during execution.

**Why this priority**: Parameters are the shared state that conditions and actions depend on. Without the ability to declare them in the editor, conditions and actions cannot be wired up or tested meaningfully.

**Independent Test**: Open the editor window with a TestGraph asset, use the parameter panel to add a bool parameter named "door_open" with default value false. Save the graph. Reload — the parameter is still present with its key and default value.

**Acceptance Scenarios**:

1. **Given** an open TestGraph editor window, **When** the developer opens the parameter panel, **Then** the existing parameters of the loaded graph are listed with their key and default value.
2. **Given** the parameter panel is open, **When** the developer adds a bool parameter with key "door_open" and default false, **Then** the parameter appears in the list and is saved with the graph asset.
3. **Given** a parameter exists in the list, **When** the developer removes it, **Then** it disappears from the list and is removed from the graph data on save.

---

### User Story 2 — Attach Conditions to Edges and Nodes (Priority: P1)

A developer attaches concrete test conditions to an edge or to a node's entry condition list in the editor. The available condition types are: `TestBoolCondition` (reads a named bool parameter and compares it to an expected value), `TestAlwaysTrueCondition`, and `TestAlwaysFalseCondition`.

**Why this priority**: Conditions are the primary branching mechanism. Without attachable conditions, the whole flow-control verification is blocked.

**Independent Test**: Create an edge between two nodes. In the inspector, add a `TestBoolCondition` to the edge with key "door_open" and expectedValue true. Save. Run the graph with the parameter at false — the edge is not traversed. Change to true — the edge is traversed.

**Acceptance Scenarios**:

1. **Given** an edge is selected or a node is selected in the inspector, **When** the developer adds a condition asset to the condition field/list, **Then** the condition appears bound to that edge or node and is saved with the graph.
2. **Given** a `TestBoolCondition` with key "door_open" and expectedValue true, **When** the graph runs with the parameter at false, **Then** the edge (or node entry) is blocked and execution follows an alternative path or stops with an "OnStuck" warning.
3. **Given** a `TestAlwaysTrueCondition` on an edge, **When** the graph runs, **Then** the edge is always traversed regardless of context state.
4. **Given** a `TestAlwaysFalseCondition` on an edge, **When** the graph runs, **Then** the edge is never traversed and execution logs an "OnStuck" warning.

---

### User Story 3 — Attach Actions to Nodes (Priority: P1)

A developer attaches concrete test actions to a node's OnEnter or OnExit action lists in the editor. The available action types are: `TestLogAction` (logs a configurable message) and `TestSetBoolAction` (writes a named bool value into the context).

**Why this priority**: Actions are the side-effect mechanism. Without verifiable actions, the developer cannot confirm that state changes propagate correctly across nodes.

**Independent Test**: Add a `TestLogAction` with message "entering node B" to a node's OnEnter list. Add a `TestSetBoolAction` setting "door_open" to true on the same node's OnExit list. Run the graph — console shows "entering node B" when the node is entered, and the parameter is true after the node is exited.

**Acceptance Scenarios**:

1. **Given** a node is selected in the inspector, **When** the developer adds a `TestLogAction` asset to the OnEnter actions list, **Then** the action appears in the list and is saved.
2. **Given** a `TestLogAction` with message "hello" on a node's OnEnter list, **When** the graph runs and that node is entered, **Then** the console shows `[GraphTest] Action: hello`.
3. **Given** a `TestSetBoolAction` setting "door_open" to true on a node's OnExit list, **When** the graph runs and that node is exited, **Then** any subsequent `TestBoolCondition` on key "door_open" evaluates to true.
4. **Given** OnEnter and OnExit actions both defined on the same node, **When** the node is visited, **Then** OnEnter actions fire before the node is considered completed, and OnExit actions fire before the next node is entered.

---

### User Story 4 — GoBack and Checkpoint Navigation (Priority: P2)

A developer uses the GoBack button in the editor toolbar to step backwards through graph execution history, and the GoBackToCheckpoint button to jump to the nearest checkpoint node. Nodes marked as checkpoints serve as save points in the execution flow.

**Why this priority**: GoBack/Checkpoint are safety-critical navigation features that can only be verified interactively through the editor. They confirm that history snapshots correctly capture context state.

**Independent Test**: Build a graph with a checkpoint node mid-way. Run to the end. Click GoBack repeatedly until returning to the checkpoint node. Verify the console logs show the correct node sequence. Click GoBackToCheckpoint from the end — verify it jumps directly to the checkpoint.

**Acceptance Scenarios**:

1. **Given** the graph has been executed at least one step, **When** the developer clicks GoBack in the toolbar, **Then** the runner steps back to the previous node and the console logs `[GraphTest] GoBack → {previousNodeType}`.
2. **Given** the runner is at the start (no history), **When** GoBack is clicked, **Then** a console message states "Nothing to go back to" and no state change occurs.
3. **Given** a node with IsCheckpoint true is in the execution history, **When** GoBackToCheckpoint is clicked, **Then** the runner restores to that checkpoint node and logs `[GraphTest] GoBack to checkpoint → {nodeType}`.
4. **Given** no checkpoint exists in history, **When** GoBackToCheckpoint is clicked, **Then** a console message states "No checkpoint in history" and no state change occurs.
5. **Given** a `TestSetBoolAction` fired during execution changed a parameter, **When** GoBack is used to return before that node, **Then** the parameter is restored to its value before the action ran.

---

### Edge Cases

- What if a `TestBoolCondition` references a parameter key that doesn't exist in the context? Execution logs a warning `[GraphTest] Parameter key not found: '{key}'` and the condition evaluates to false.
- What if two edges from the same node both have conditions that evaluate to true? The first matching edge in declaration order is selected (consistent with `BaseRunner.SelectEdge` behavior).
- What if GoBack is called while execution is in `Ended` state? The runner steps back to the last visited node and resumes from `NodeReady` state.
- What if a condition or action asset is deleted from the project while still referenced by a graph? The reference becomes null — execution skips null entries with a warning rather than throwing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The package MUST provide three concrete condition types: `TestBoolCondition`, `TestAlwaysTrueCondition`, and `TestAlwaysFalseCondition`, each creatable as a project asset.
- **FR-002**: The package MUST provide two concrete action types: `TestLogAction` and `TestSetBoolAction`, each creatable as a project asset.
- **FR-003**: `TestBoolCondition` MUST expose a configurable parameter key and an expected bool value. It MUST evaluate by reading the context and returning whether the stored value matches the expected value. When the key is absent, it MUST evaluate to false and log a warning.
- **FR-004**: `TestAlwaysTrueCondition` MUST always return true regardless of context state. `TestAlwaysFalseCondition` MUST always return false.
- **FR-005**: `TestLogAction` MUST expose a configurable message string and log `[GraphTest] Action: {message}` to the console when executed.
- **FR-006**: `TestSetBoolAction` MUST expose a configurable parameter key and bool value, and write that value into the context when executed.
- **FR-007**: The editor window MUST include a parameter panel that allows the developer to add, view, and remove bool parameters on the loaded graph, with changes persisting on save.
- **FR-008**: The inspector panel MUST allow attaching condition assets to edge condition fields and to node entry condition lists, and action assets to node OnEnter and OnExit action lists.
- **FR-009**: The editor toolbar MUST include a GoBack button that calls the runner's step-back function and logs the result to the console.
- **FR-010**: The editor toolbar MUST include a GoBackToCheckpoint button that calls the runner's checkpoint-restore function and logs the result to the console.
- **FR-011**: GoBack and GoBackToCheckpoint MUST be no-ops when no runner session is active, with a console message explaining why.
- **FR-012**: Null condition or action references MUST be skipped silently during execution, with a `[GraphTest]` warning logged rather than an exception thrown.

### Key Entities

- **TestBoolCondition**: Condition asset — reads a named bool parameter from context and compares it to an expected value.
- **TestAlwaysTrueCondition**: Condition asset — unconditionally passes.
- **TestAlwaysFalseCondition**: Condition asset — unconditionally blocks.
- **TestLogAction**: Action asset — logs a configurable message when executed.
- **TestSetBoolAction**: Action asset — writes a named bool value into the context when executed.
- **ParameterPanel**: Editor UI component — lists, adds, and removes `ParameterData` entries on the loaded graph.
- **RunnerSession**: In-memory execution state held by the editor window between Run, GoBack, and GoBackToCheckpoint calls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All three condition types and both action types can be created as assets from the Project window's Create menu without errors.
- **SC-002**: A graph using `TestSetBoolAction` (OnExit of node A) and `TestBoolCondition` (on the edge from node A to node B) executes correctly: node B is reached when the parameter matches, and the "OnStuck" warning appears when it does not.
- **SC-003**: A graph with a checkpoint node: after running to completion, GoBackToCheckpoint restores execution to the checkpoint in a single click, and the parameter panel reflects the restored context state.
- **SC-004**: All `TestLogAction` messages appear in the console in the exact order nodes are visited during a full graph run.
- **SC-005**: GoBack and GoBackToCheckpoint buttons appear in the toolbar and respond in under 1 second for any graph with fewer than 100 history entries.
- **SC-006**: Adding and removing bool parameters in the parameter panel and saving/reloading the graph preserves all parameter changes with 100% fidelity.

## Assumptions

- Condition and action assets are `ScriptableObject` instances created as `.asset` files in the project, then assigned via the inspector — no inline creation.
- The `RunnerSession` (runner + context) persists in the editor window between Run, GoBack, and GoBackToCheckpoint button presses within the same window session; closing and reopening the window resets it.
- Only bool parameters are exposed in the parameter panel for this feature — int, float, and string are out of scope.
- Cycle detection on the graph during Run is already handled by `BaseRunner` (feature 005) and is not re-implemented here.
- The parameter panel is embedded in the existing editor window layout, not a separate floating window.
- GoBack/GoBackToCheckpoint only become active after at least one Run has been started in the current window session.
