---
description: "Task list for GraphTest — Choices & ChooseById"
---

# Tasks: GraphTest — Choices & ChooseById

**Input**: Design documents from `specs/007-graphtest-choices/`

**Prerequisites**: plan.md (required), spec.md (required), data-model.md

**Tests**: INCLUDED — Constitution Principle IV mandates TDD (Red-Green-Refactor). Test tasks are written first and must FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths are relative to the package root `com.faolline.graphTest/`

## Path Conventions

This is an extension of the existing downstream package `com.faolline.graphTest`. No new assembly definitions are created. Runtime code lives under `Runtime/`, editor code under `Editor/`, and EditMode tests under `Tests/EditMode/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the new folder for the runtime choice type. No new dependencies or asmdefs (extension of existing package).

- [x] T001 Create the `com.faolline.graphTest/Runtime/Choices/` directory for the new `TestChoice` type

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the `TestChoice` runtime entity that every user story depends on (authoring, execution, and conditional filtering all reference it).

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Write failing test for `TestChoice` (label round-trips, inherits `Id`/`Condition` from `BaseChoice`, is `[Serializable]`) in `com.faolline.graphTest/Tests/EditMode/Runtime/TestChoiceTests.cs`
- [x] T003 Implement `TestChoice : BaseChoice` with `[SerializeField] private string _label;` and public `string Label { get; set; }` in `com.faolline.graphTest/Runtime/Choices/TestChoice.cs`

**Checkpoint**: `TestChoice` compiles and its tests pass — user stories can now begin

---

## Phase 3: User Story 1 — Author a Choice Node (Priority: P1) 🎯 MVP

**Goal**: Add a `ChoiceNodeData` to the canvas via the context menu, manage its choices (add/remove/edit label & condition) in the inspector, and have one output port per choice (port name = choice `Id`, displayed label = choice `Label`). Choices and their edges survive save/reload.

**Independent Test**: Right-click canvas → "Add Choice Node". In the inspector add two choices ("Go left", "Go right"), draw edges from each port to a target node, save and reload — both choices and edges persist.

### Tests for User Story 1 ⚠️ (write first, must FAIL before implementation)

- [x] T004 [P] [US1] Write failing test for `ChoiceNodeView`: one `"in"` input port, zero output ports when no choices, and `RebuildPorts()` produces one output port per choice with `portName == choice.Id` and displayed label == `choice.Label`, in `com.faolline.graphTest/Tests/EditMode/Editor/ChoiceNodeViewTests.cs`
- [x] T005 [P] [US1] Write failing test for "Add Choice Node" context-menu entry creating a `ChoiceNodeData` and `CreateNodeView` dispatching to a `ChoiceNodeView`, in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphViewAddNodeTests.cs`
- [x] T006 [P] [US1] Write failing test for the inspector Choice section: Add Choice appends a `TestChoice` (new GUID, default label) and triggers a port rebuild; Remove drops the choice and its bound edge; label/condition edits write back to the data — in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorViewTests.cs`
- [x] T007 [P] [US1] Write failing save/reload fidelity test: a `ChoiceNodeData` with two `TestChoice` entries and two connected edges round-trips with both choices and edges intact, in `com.faolline.graphTest/Tests/EditMode/Runtime/TestGraphTests.cs`

### Implementation for User Story 1

- [x] T008 [US1] Implement `ChoiceNodeView : BaseNodeView` with the `"in"` input port (`Port.Capacity.Multi`, typed `TestEdgeView`) and `RebuildPorts()` that clears `outputContainer` and recreates one `Port.Capacity.Single` output per choice — `portName = choice.Id`, displayed connector label overridden to `choice.Label` (R-002) — in `com.faolline.graphTest/Editor/Nodes/ChoiceNodeView.cs`
- [x] T009 [US1] Add "Add Choice Node" to `BuildContextualMenu` (creates `ChoiceNodeData`) and a `CreateNodeView` case `ChoiceNodeData.NodeTypeId → new ChoiceNodeView(...)`, holding the view reference for inspector-driven rebuilds, in `com.faolline.graphTest/Editor/Graph/TestGraphView.cs`
- [x] T010 [US1] Implement the inspector Choice section in `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs`: render when selected node is `ChoiceNodeData`; "Add Choice" button (append `TestChoice` w/ new GUID + default label, call `ChoiceNodeView.RebuildPorts()`); per-choice row with label `TextField`, condition `ObjectField` (`BaseCondition`), and Remove button (drop choice, rebuild ports, remove edge whose `PortName == choice.Id`)
- [x] T011 [US1] Wire the inspector → view port-rebuild path in `com.faolline.graphTest/Editor/Graph/TestGraphView.cs` (and inspector wiring in `TestNodeInspectorView.cs`) so add/remove updates the canvas without a full `LoadGraph` (R-004)

**Checkpoint**: A Choice node can be authored, edited, connected, saved, and reloaded with full fidelity (SC-001)

---

## Phase 4: User Story 2 — Run to a Choice Node and Select a Choice (Priority: P1)

**Goal**: On Run, execution proceeds until it reaches a `ChoiceNodeData`, then pauses with a `_waitingForChoice` flag and logs `[GraphTest] Waiting for choice at node: {nodeId}`. The Choose toolbar button becomes active and lists choices; selecting one calls `runner.ChooseById(choiceId)` and resumes the drain loop. Choose is inactive/no-op when no session is paused at a choice.

**Independent Test**: Build Start → Choice ("Left","Right") → Statement A / Statement B → End. Run pauses at Choice with the waiting log. Choose → "Left" visits A → End; rerun and choose "Right" visits B.

### Tests for User Story 2 ⚠️ (write first, must FAIL before implementation)

- [x] T012 [P] [US2] Write failing test: running to a `ChoiceNodeData` pauses the loop, sets `_waitingForChoice`/`_waitingChoiceNode`, and logs `[GraphTest] Waiting for choice at node: {nodeId}` without calling `Proceed()`, in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphExecutionTests.cs`
- [x] T013 [P] [US2] Write failing test: `Choose(choiceId)` while paused clears the flag, calls `runner.ChooseById(choiceId)`, resumes the drain loop, and routes to the correct branch for both "Left" and "Right"; and `Choose` when not paused / after `Ended` logs `No active choice — click Run first` with no exception (SC-002, SC-005), in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphEditorWindowSessionTests.cs`

### Implementation for User Story 2

- [x] T014 [US2] Add `_waitingForChoice` (`bool`) and `_waitingChoiceNode` (`ChoiceNodeData`) fields and refactor `ExecuteGraph` into a `DrainLoop()` that, on `CurrentNode is ChoiceNodeData`, sets the flags, logs the waiting message, and returns without `Proceed()` (D-005), in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs`
- [x] T015 [US2] Implement `public void Choose(string choiceId)` — guard with the no-op message when `!_waitingForChoice`, otherwise clear the flag, call `runner.ChooseById(choiceId)`, and re-enter `DrainLoop()` (D-005, FR-008, FR-011), in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs`
- [x] T016 [US2] Add the "Choose" `ToolbarButton` that, while `_waitingForChoice`, opens a dropdown of choice labels and calls `Choose(choice.Id)` on selection; inactive/no-op otherwise (D-006, FR-007, FR-011), in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs`

**Checkpoint**: Run pauses at a choice and resumes correctly on the selected branch across reruns (SC-002, SC-005)

---

## Phase 5: User Story 3 — Conditional Choices (Priority: P2)

**Goal**: At runtime, only choices whose `Condition` is null or evaluates true against the live `_activeContext` are offered. If none pass, the runner is stuck and execution halts with the standard warning.

**Independent Test**: Choice node with "Open door" (`TestBoolCondition` key="door_open" expected=true) and "Leave" (no condition). With `door_open=false` only "Leave" is selectable; with `door_open=true` both are.

### Tests for User Story 3 ⚠️ (write first, must FAIL before implementation)

- [x] T017 [P] [US3] Write failing test: the Choose list filters out a choice with `TestAlwaysFalseCondition` and keeps one with `TestAlwaysTrueCondition`/null condition, evaluated against `_activeContext` (SC-003, FR-009), in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphEditorWindowSessionTests.cs`
- [x] T018 [P] [US3] Write failing test: when all choices fail their conditions (or the node has no choices), execution halts and logs `[GraphTest] Execution stopped: runner is stuck` without calling `ChooseById` (FR-010, edge cases), in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphExecutionTests.cs`

### Implementation for User Story 3

- [x] T019 [US3] Implement choice-availability filtering (`choice.Condition == null || choice.Condition.Evaluate(_activeContext)`) for the Choose dropdown, and halt with the stuck warning when the filtered list is empty (R-003, FR-009, FR-010), in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs`

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Edge-case correctness and final validation across stories

- [x] T020 [P] Write and satisfy a test for GoBack while paused at a choice: `_waitingForChoice` clears and the runner restores the previous node (FR-012, SC-004, edge case), in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphEditorWindowSessionTests.cs` and the GoBack handler in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs`
- [x] T021 Run the full EditMode suite via Unity Test Runner and confirm all 007 tests are green (Red-Green-Refactor complete) — 70/70 passed (batchmode, 6000.3.6f1)
- [ ] T022 Validate the spec's Independent Tests manually in the editor (US1 author/save/reload, US2 pause/choose both branches, US3 conditional filtering)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (every story uses `TestChoice`)
- **User Stories (Phase 3–5)**: All depend on Foundational completion
  - US1 (P1) and US2 (P1) are both MVP-critical; US2's execution pause is most easily verified once US1 authoring exists, so the recommended order is US1 → US2 → US3
- **Polish (Phase 6)**: Depends on US1–US3 being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational — authoring is self-contained
- **US2 (P1)**: Depends on Foundational; uses Choice nodes authored via US1 to build its test graphs (test-data dependency, not a code dependency)
- **US3 (P2)**: Depends on Foundational and reuses US2's Choose-list path to apply condition filtering

### Within Each User Story

- Tests are written first and MUST FAIL before implementation (Constitution IV)
- Runtime/data before views; views before window/execution wiring
- Story complete and green before moving to the next priority

### Parallel Opportunities

- T002 and the US1 test tasks T004–T007 are all `[P]` (distinct files) and can be drafted in parallel
- US2 tests T012–T013 and US3 tests T017–T018 are `[P]` within their stories
- Implementation tasks T014–T016 and T019 all touch `TestGraphEditorWindow.cs`, so they are **sequential** (not `[P]`)

---

## Parallel Example: User Story 1

```text
# Launch the US1 test tasks together (different files):
Task: "T004 ChoiceNodeView tests in Tests/EditMode/Editor/ChoiceNodeViewTests.cs"
Task: "T005 Add Choice Node context-menu test in Tests/EditMode/Editor/TestGraphViewAddNodeTests.cs"
Task: "T006 Inspector Choice-section test in Tests/EditMode/Editor/TestNodeInspectorViewTests.cs"
Task: "T007 Save/reload fidelity test in Tests/EditMode/Runtime/TestGraphTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1: Setup
2. Phase 2: Foundational (`TestChoice`) — CRITICAL, blocks all stories
3. Phase 3: User Story 1 — author/save/reload choice nodes
4. **STOP and VALIDATE**: Author two choices, connect, save, reload (SC-001)

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → authoring works → validate (MVP)
3. US2 → pause/resume at choices → validate both branches
4. US3 → conditional filtering → validate
5. Polish → GoBack edge case + full-suite + manual validation

---

## Notes

- [P] = different files, no dependencies
- The window file `Editor/Window/TestGraphEditorWindow.cs` is the shared hot-spot for US2/US3 — keep those tasks sequential
- No graphcore changes: `ChoiceNodeData`, `BaseChoice`, and `BaseRunner.ChooseById` already exist
- Commit after each task or logical group
- Copy/paste and undo/redo of choice nodes are out of scope (per spec Assumptions)
