# Tasks: GraphTest — Conditions, Actions & Checkpoints

**Input**: Design documents from `specs/006-graphtest-conditions-actions/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | data-model.md ✅

**TDD Note**: Constitution Principle IV is mandatory. Every test task MUST be run via
Coplay `run_tests` to confirm failure BEFORE the matching implementation task begins.

---

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase
- **[Story]**: User story this task belongs to (US1–US4)

---

## Phase 1: Setup

**Purpose**: Create the missing subdirectories in the existing package.

- [X] T001 Create `com.faolline.graphTest/Runtime/Conditions/` and `com.faolline.graphTest/Runtime/Actions/` directories

**Checkpoint**: Directories exist. Package still compiles.

---

## Phase 2: Foundational — Graphcore Sub-Tasks

**Purpose**: Two MINOR additive changes to `com.faolline.graphcore` required before any user story work.
**⚠️ CRITICAL**: No user story implementation can begin until T002–T005 are complete.

### Sub-task A — `BaseRunner.CurrentNode`

- [X] T002 Write failing EditMode test `CurrentNode_ReturnsActiveNode` in `com.faolline.graphcore/Tests/EditMode/Execution/BaseRunnerCurrentNodeTests.cs` — starts a runner on a linear graph, asserts `CurrentNode` returns the first visited `BaseNodeData`. Run via Coplay `run_tests` and confirm failure before T003.
- [X] T003 Add `public BaseNodeData CurrentNode` property to `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs` — peeks `_graphStack`, calls `FindNode(frame.Graph, frame.CurrentNodeId)`. Returns null when stack is empty or `State == Idle`.

### Sub-task B — Null guards in `BaseRunner`

- [X] T004 Write failing EditMode tests `EntryCondition_Null_IsSkipped` and `OnEnterAction_Null_IsSkipped` in `com.faolline.graphcore/Tests/EditMode/Execution/BaseRunnerNullSafetyTests.cs` — build a graph where a node's `EntryConditions` contains a null entry, assert execution completes without exception. Run via Coplay `run_tests` and confirm failure before T005.
- [X] T005 Add null guards before `condition.Evaluate()` in `EnterCurrentNode` and before `action.Execute()` in `EnterCurrentNode`/`ExitAndAdvance` in `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs` — skip null entries with `Debug.LogWarning("[GraphCore] Null condition/action entry skipped on node '{id}'.")`.

**Checkpoint**: Both graphcore sub-tasks complete, all graphcore tests green.

---

## Phase 3: User Story 1 — Declare Graph Parameters (Priority: P1)

**Goal**: The inspector panel shows a bool parameter list when no node is selected, with Add/Remove controls. Parameter changes persist with the graph asset.

**Independent Test**: Load a TestGraph, ensure no node is selected — inspector shows "Parameters" foldout. Add parameter "door_open" (bool, default false). Save and reload — parameter still present.

- [X] T006 [US1] Write failing EditMode tests `ClearInspector_WithGraph_ShowsParameterPanel` and `ParameterPanel_AddBoolParam_PersistsInGraph` in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorParameterPanelTests.cs`. Run via Coplay `run_tests` and confirm failure before T007.
- [X] T007 [US1] Update `ClearInspector()` in `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs` — when `_graph != null`, build a "Parameters" foldout listing bool parameters (`param.Type == ParameterType.Bool`) with their key and default value displayed; add a button "Add Bool Parameter" that appends a new `ParameterData {Key="new_param", Type=Bool, DefaultValue="False"}` to `_graph.Parameters` and calls `EditorUtility.SetDirty(_graph)`; add a Remove button per entry.

**Checkpoint**: Inspector shows parameter panel when nothing is selected. Add/Remove works. T006 tests pass.

---

## Phase 4: User Story 2 — Attach Conditions to Edges and Nodes (Priority: P1)

**Goal**: Three concrete condition assets creatable from the Project window. Each evaluates correctly when assigned to an edge or node entry condition list.

**Independent Test**: Create `TestBoolCondition` with key "door_open" expectedValue true. Assign to an edge. Run graph with context param at false → edge blocked (OnStuck logged). Change default to true → edge traversed.

- [X] T008 [P] [US2] Write failing EditMode tests for `TestBoolCondition` in `com.faolline.graphTest/Tests/EditMode/Runtime/ConditionTests.cs`:
  - `TestBoolCondition_KeyTrue_ReturnsTrue` — context has key=true, expectedValue=true → true
  - `TestBoolCondition_KeyFalse_ReturnsFalse` — context has key=false, expectedValue=true → false
  - `TestBoolCondition_MissingKey_ReturnsFalseWithWarning`
  - `TestAlwaysTrueCondition_AlwaysReturnsTrue`
  - `TestAlwaysFalseCondition_AlwaysReturnsFalse`
  Run via Coplay `run_tests` and confirm failure before T009–T011.
- [X] T009 [P] [US2] Create `com.faolline.graphTest/Runtime/Conditions/TestBoolCondition.cs` — `TestBoolCondition : BaseCondition` with `[CreateAssetMenu(menuName = "GraphTest/Conditions/Bool Condition")]`; `[SerializeField] private string _parameterKey`; `[SerializeField] private bool _expectedValue`; `Evaluate()` calls `context.TryGet<bool>(_parameterKey, out var v)`, returns `v == _expectedValue`, logs warning on missing key.
- [X] T010 [P] [US2] Create `com.faolline.graphTest/Runtime/Conditions/TestAlwaysTrueCondition.cs` — `TestAlwaysTrueCondition : BaseCondition` with `[CreateAssetMenu(menuName = "GraphTest/Conditions/Always True")]`; `Evaluate()` returns `true`.
- [X] T011 [P] [US2] Create `com.faolline.graphTest/Runtime/Conditions/TestAlwaysFalseCondition.cs` — `TestAlwaysFalseCondition : BaseCondition` with `[CreateAssetMenu(menuName = "GraphTest/Conditions/Always False")]`; `Evaluate()` returns `false`.

**Checkpoint**: All three condition types compile and are createable as assets. T008 tests pass.

---

## Phase 5: User Story 3 — Attach Actions to Nodes (Priority: P1)

**Goal**: Two concrete action assets creatable from the Project window. `TestLogAction` logs to console; `TestSetBoolAction` writes a bool into the context so downstream conditions can read it.

**Independent Test**: Assign `TestSetBoolAction` (key="door_open", value=true) to node A's OnExit. Assign `TestBoolCondition` (key="door_open", expectedValue=true) to the edge A→B. Run — node A exits, edge condition passes, node B is entered.

- [X] T012 [P] [US3] Write failing EditMode tests for both action types in `com.faolline.graphTest/Tests/EditMode/Runtime/ActionTests.cs`:
  - `TestLogAction_Execute_LogsMessage` — verify `Debug.Log("[GraphTest] Action: hello")` fires
  - `TestSetBoolAction_Execute_WritesValueToContext` — verify `context.TryGet<bool>(key)` returns the set value after Execute
  Run via Coplay `run_tests` and confirm failure before T013–T014.
- [X] T013 [P] [US3] Create `com.faolline.graphTest/Runtime/Actions/TestLogAction.cs` — `TestLogAction : BaseAction` with `[CreateAssetMenu(menuName = "GraphTest/Actions/Log Action")]`; `[SerializeField] private string _message`; `Execute()` calls `Debug.Log($"[GraphTest] Action: {_message}")`.
- [X] T014 [P] [US3] Create `com.faolline.graphTest/Runtime/Actions/TestSetBoolAction.cs` — `TestSetBoolAction : BaseAction` with `[CreateAssetMenu(menuName = "GraphTest/Actions/Set Bool Action")]`; `[SerializeField] private string _parameterKey`; `[SerializeField] private bool _value`; `Execute()` calls `context.Set<bool>(_parameterKey, _value)`.

**Checkpoint**: Both action types compile and are creatable as assets. T012 tests pass.

---

## Phase 6: User Story 4 — GoBack and Checkpoint Navigation (Priority: P2)

**Goal**: Runner session persists after Run completes. GoBack steps back through history. GoBackToCheckpoint jumps to the nearest checkpoint. All actions log to the console.

**Independent Test**: Build a graph with a checkpoint mid-way. Run to end. Click GoBack — console shows previous node type. Click GoBackToCheckpoint — console shows checkpoint node type. Click GoBack when nothing is left — "nothing to go back to" message.

- [X] T015 [US4] Write failing EditMode tests for session and navigation in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphEditorWindowSessionTests.cs`:
  - `ExecuteGraph_SetsActiveSession`
  - `GoBack_WithNoSession_LogsWarning`
  - `GoBack_WithSession_DecrementsHistory`
  Run via Coplay `run_tests` and confirm failure before T016.
- [X] T016 [US4] Add session fields to `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs` — add `private BaseRunner _activeRunner`, `private BaseContext _activeContext`, `private bool _hasActiveSession`; update `ExecuteGraph` to store runner and context as fields after `runner.Start()` succeeds and set `_hasActiveSession = true`; new Run always resets the session.
- [X] T017 [US4] Add GoBack and GoBackToCheckpoint toolbar buttons to `PopulateToolbar` in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs` — GoBack: if `!_hasActiveSession` log warning, else call `_activeRunner.GoBack()` and log `$"[GraphTest] GoBack → {_activeRunner.CurrentNode?.NodeType ?? "nothing to go back to"}"`; GoBackToCheckpoint: same pattern with `GoBackToCheckpoint()`.

**Checkpoint**: Run → session stored. GoBack → steps back with log. GoBackToCheckpoint → jumps to checkpoint. T015 tests pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T018 [P] Add XML `<summary>` doc comments to all new public members in `com.faolline.graphTest/Runtime/Conditions/` and `com.faolline.graphTest/Runtime/Actions/` (5 files)
- [ ] T019 Run full graphcore EditMode test suite via Coplay `run_tests` — all green, zero regressions from Phase 2 changes
- [ ] T020 Run full `com.faolline.graphTest` EditMode test suite via Coplay `run_tests` — all green
- [ ] T021 Manual integration smoke test: build a graph with TestSetBoolAction on node A's OnExit, TestBoolCondition on edge A→B, TestAlwaysFalseCondition on edge A→C, a checkpoint node, and run it — verify SC-001 through SC-006 from the spec

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1. **BLOCKS all user story phases.**
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2. Independent of Phase 3.
- **Phase 5 (US3)**: Depends on Phase 2. Independent of Phase 3 and 4.
- **Phase 6 (US4)**: Depends on Phase 2 + needs `BaseRunner.CurrentNode` (T003). Independent of US1–US3 for toolbar; US3 enhances the test scenario.
- **Phase 7 (Polish)**: Depends on Phases 3–6 complete.

### User Story Dependencies

- **US1 (P1)**: Independent after Phase 2
- **US2 (P1)**: Independent after Phase 2 — T009/T010/T011 fully parallelizable
- **US3 (P1)**: Independent after Phase 2 — T013/T014 fully parallelizable
- **US4 (P2)**: Needs `BaseRunner.CurrentNode` (T003) for logging

### Within Each Phase

1. Write test → run via Coplay `run_tests` → confirm FAILURE
2. Implement → run via Coplay `run_tests` → confirm PASS
3. Commit atomically before moving to next task

---

## Parallel Opportunities

### Phase 4 (US2 — T009/T010/T011): All three condition types in parallel

```
T009: TestBoolCondition.cs
T010: TestAlwaysTrueCondition.cs
T011: TestAlwaysFalseCondition.cs
```

### Phase 5 (US3 — T013/T014): Both action types in parallel

```
T013: TestLogAction.cs
T014: TestSetBoolAction.cs
```

### Phase 7 (T018/T019/T020): XML docs and test runs in parallel

```
T018: XML doc comments
T019: graphcore test suite
T020: graphTest test suite
```

---

## Implementation Strategy

### MVP (US2 + US3 only — Phases 1 + 2 + 4 + 5)

1. Phase 1: Create directories
2. Phase 2: Graphcore sub-tasks
3. Phase 4: Three condition types
4. Phase 5: Two action types
5. **STOP and VALIDATE**: Assign a `TestBoolCondition` to an edge, run graph — verify gate works

### Incremental Delivery

1. MVP → conditions and actions creatable and functional ✅
2. Add Phase 3 → parameter panel in inspector ✅
3. Add Phase 6 → GoBack/GoBackToCheckpoint in toolbar ✅
4. Phase 7 → polish and final validation ✅

---

## Notes

- Condition/action assets are `ScriptableObject` — tests instantiate them via `ScriptableObject.CreateInstance<T>()` and destroy via `Object.DestroyImmediate` in `[TearDown]`
- `TestLogAction_Execute_LogsMessage` uses `LogAssert.Expect(LogType.Log, ...)` from `UnityEngine.TestTools`
- `BaseRunner.CurrentNode` returns null after `GoBack()` depletes all history — the GoBack log handles this with the null-coalescing branch
- GoBack/GoBackToCheckpoint buttons should be greyed out (disabled) when `!_hasActiveSession` — use `toolbar.SetEnabled(false)` on the button element or check in the click handler
