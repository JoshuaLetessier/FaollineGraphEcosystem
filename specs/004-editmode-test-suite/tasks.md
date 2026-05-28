# Tasks: EditMode Test Suite

**Input**: Design documents from `specs/004-editmode-test-suite/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Note**: This feature's deliverable IS the test code. All five fixtures replace their
ad-hoc predecessors with canonical, single-responsibility classes following the
`MethodName_Scenario_ExpectedResult` convention. Constitution Principle IV applies:
each test must be run via `run_tests` to confirm it passes before moving to the next task.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup

**Purpose**: Remove legacy fixture files that are superseded by the canonical fixtures
created in phases 3–7. Removing first prevents duplicate test class names in the assembly.

- [x] T001 Delete `Tests/EditMode/Execution/BaseRunnerLinearTests.cs` (superseded by BaseRunnerTests)
- [x] T002 [P] Delete `Tests/EditMode/Execution/BaseContextTests.cs` (superseded by BaseContextTests with canonical class name — note: `DataLayer/BaseContextTests.cs` is out-of-scope, kept)
- [x] T003 [P] Delete `Tests/EditMode/Execution/BaseRunnerHistoryTests.cs` (superseded by HistoryTests)
- [x] T004 [P] Delete `Tests/EditMode/Execution/BaseRunnerSubGraphTests.cs` (superseded by SubGraphTests + CycleDetectionTests)

**Checkpoint**: Old fixture files removed; assembly compiles with zero errors before new files are added.

---

## Phase 2: Foundational

**Purpose**: No new infrastructure needed — existing asmdef, directory, and Unity Test Framework
already cover all five fixtures. This phase is intentionally empty.

**⚠️ CRITICAL**: Confirm assembly compiles clean after Phase 1 before beginning any user story.

**Checkpoint**: `run_tests` returns zero compilation errors; zero test failures (remaining DataLayer and Editor tests still pass).

---

## Phase 3: User Story 1 — BaseRunner State Machine Coverage (Priority: P1) 🎯 MVP

**Goal**: A single `BaseRunnerTests.cs` fixture covers all `RunnerState` transitions, entry/exit
action sequencing, executor dispatch, stuck conditions, and choice selection.

**Independent Test**: Run `BaseRunnerTests` fixture in isolation in Unity Test Runner → all tests green.

### Implementation

- [x] T005 [US1] Create `Tests/EditMode/Execution/BaseRunnerTests.cs` with namespace `Faolline.GraphCore.Tests`, `[SetUp]` building a linear Start→Statement→End graph, `[TearDown]` destroying all ScriptableObjects, and inner stubs `TrackingAction`, `ConstantCondition`, `LambdaExecutor`
- [x] T006 [US1] Add `Start_*` test group to `BaseRunnerTests.cs`: `Start_ValidGraph_TransitionsToNodeReady`, `Start_MissingEntryNodeId_ThrowsInvalidOperationException`, `Start_ValidGraph_RaisesOnNodeEntered`, `Start_ValidGraph_RaisesOnNodeCompleted`
- [x] T007 [US1] Add `Proceed_*` test group to `BaseRunnerTests.cs`: `Proceed_FromNodeReady_AdvancesToNextNode`, `Proceed_ExecutesFullNodeLifecycleOrder` (enter-actions → executor → OnNodeCompleted), `Proceed_OnExitActions_RunBeforeNextNodeEntered`, `Proceed_ReachesEndNode_TransitionsToEnded`, `Proceed_WhenEnded_IsNoOp`
- [x] T008 [US1] Add `OnEnded_*` test group to `BaseRunnerTests.cs`: `OnEnded_RaisedWithCorrectEndReason`
- [x] T009 [US1] Add `EntryCondition_*` test group to `BaseRunnerTests.cs`: `EntryCondition_AllPass_NodeExecutes`, `EntryCondition_Fails_RaisesOnStuck`, `EntryCondition_Fails_RunnerStaysNodeReady`
- [x] T010 [US1] Add `ChooseById_*` test group to `BaseRunnerTests.cs`: `ChooseById_SelectsEdgeByEdgeId`, `ChooseById_SelectsEdgeByPortName`, `ChooseById_NoMatchingEdge_RaisesOnStuck`
- [x] T011 [US1] Add `Execute_*` test group to `BaseRunnerTests.cs`: `Execute_NoRegisteredExecutor_DoesNotThrow`, `Execute_RegisteredExecutor_IsCalled`, `Execute_RegisteredExecutor_CalledWithCorrectNodeAndContext`
- [ ] T012 [US1] Run `run_tests` on `BaseRunnerTests` fixture and confirm all tests pass; fix any failures before proceeding

**Checkpoint**: `BaseRunnerTests` fully green. All `RunnerState` values, all node lifecycle steps, all event paths covered.

---

## Phase 4: User Story 2 — BaseContext Typed Blackboard Coverage (Priority: P1)

**Goal**: A single `BaseContextTests.cs` fixture covers all four supported types, event
subscription/unsubscription, and `DeepClone` value-only semantics.

**Independent Test**: Run `BaseContextTests` fixture in isolation → all tests green.

### Implementation

- [x] T013 [P] [US2] Create `Tests/EditMode/Execution/BaseContextTests.cs` with namespace `Faolline.GraphCore.Tests` and no shared mutable fields (all contexts created locally per test)
- [x] T014 [US2] Add `Set_Get_*` test group to `BaseContextTests.cs`: `Set_Get_Bool_ReturnsCorrectValue`, `Set_Get_Int_ReturnsCorrectValue`, `Set_Get_Float_ReturnsCorrectValue`, `Set_Get_String_ReturnsCorrectValue`, `Set_OverwritesExistingValue`, `Set_UnsupportedType_ThrowsArgumentException`
- [x] T015 [US2] Add `TryGet_*` and `Has_*` test group to `BaseContextTests.cs`: `TryGet_ExistingKey_ReturnsTrueAndValue`, `TryGet_MissingKey_ReturnsFalseAndDefault`, `Has_ExistingKey_ReturnsTrue`, `Has_MissingKey_ReturnsFalse`, `Get_MissingKey_ThrowsKeyNotFoundException`
- [x] T016 [US2] Add `OnParameterChanged_*` test group to `BaseContextTests.cs`: `OnParameterChanged_FiredOnSet`, `OnParameterChanged_NotFiredForDifferentKey`, `OffParameterChanged_NotFiredAfterUnsubscribe`, `OnParameterChanged_FiresOnFirstSet`
- [x] T017 [US2] Add `DeepClone_*` test group to `BaseContextTests.cs`: `DeepClone_CopiesAllValues`, `DeepClone_MutatingOriginalDoesNotAffectClone`, `DeepClone_MutatingCloneDoesNotAffectOriginal`, `DeepClone_DoesNotCopySubscriptions`, `DeepClone_EmptyContext_ReturnsValidContext`
- [ ] T018 [US2] Run `run_tests` on `BaseContextTests` fixture and confirm all tests pass; fix any failures before proceeding

**Checkpoint**: `BaseContextTests` fully green. All four `ParameterType` values covered; clone isolation confirmed.

---

## Phase 5: User Story 3 — History Integrity Coverage (Priority: P1)

**Goal**: A single `HistoryTests.cs` fixture covers snapshot correctness, `GoBack`,
`GoBackToCheckpoint`, depth-cap enforcement, and unlimited depth.

**Independent Test**: Run `HistoryTests` fixture in isolation → all tests green.

### Implementation

- [x] T019 [P] [US3] Create `Tests/EditMode/Execution/HistoryTests.cs` with namespace `Faolline.GraphCore.Tests`, `List<BaseGraph> _graphs` field, `Track(BaseGraph)` helper, `[TearDown]` destroying all tracked graphs, `BuildChainGraph(int count)` helper (n0=Start, n(last)=End, middle=Statement), and inner `LambdaExecutor` stub
- [x] T020 [US3] Add `GoBack_*` test group to `HistoryTests.cs`: `GoBack_RestoresPreviousNode`, `GoBack_RestoresContextValues`, `GoBack_EmptyHistory_IsNoOp`, `GoBack_TruncatesHistoryFromRestoredEntry`
- [x] T021 [US3] Add `GoBackToCheckpoint_*` test group to `HistoryTests.cs`: `GoBackToCheckpoint_RestoresNearestCheckpointNode`, `GoBackToCheckpoint_NoCheckpointInHistory_IsNoOp`, `GoBackToCheckpoint_MultipleCheckpoints_RestoresMostRecent`
- [x] T022 [US3] Add `History_CappedByHistoryDepth_*` test group to `HistoryTests.cs`: `History_CappedByHistoryDepth_EvictsOldestEntry`, `History_CappedByHistoryDepth_ExtraGoBackIsNoOp`
- [x] T023 [US3] Add `History_DepthZero_*` test group to `HistoryTests.cs`: `History_DepthZero_AllAdvancesUndoable`
- [ ] T024 [US3] Run `run_tests` on `HistoryTests` fixture and confirm all tests pass; fix any failures before proceeding

**Checkpoint**: `HistoryTests` fully green. Cap enforcement at depth N and unlimited at depth 0 both verified.

---

## Phase 6: User Story 4 — SubGraph Stack and Context Coverage (Priority: P1)

**Goal**: A single `SubGraphTests.cs` fixture covers stack push/pop, context inheritance
and isolation, null target guard, nested sub-graphs, and `OnEnded` propagation.

**Independent Test**: Run `SubGraphTests` fixture in isolation → all tests green.

### Implementation

- [x] T025 [P] [US4] Create `Tests/EditMode/Execution/SubGraphTests.cs` with namespace `Faolline.GraphCore.Tests`, `List<BaseGraph> _graphs` field, `Track(BaseGraph)` helper, `[TearDown]` destroying all tracked graphs, `BuildLinearGraph(string entryId, string endId)` helper, and inner `LambdaExecutor` stub
- [x] T026 [US4] Add `SubGraph_Push_*` test group to `SubGraphTests.cs`: `SubGraph_Push_EntersChildGraphEntryNode`, `SubGraph_Push_ChildNodesVisitedInOrder`
- [x] T027 [US4] Add `SubGraph_Pop_*` test group to `SubGraphTests.cs`: `SubGraph_Pop_ChildEndNode_ResumesParent`, `SubGraph_Pop_ParentCompletesNormally`, `SubGraph_Pop_OnEndedFiresExactlyOnce`
- [x] T028 [US4] Add `SubGraph_InheritContext_*` test group to `SubGraphTests.cs`: `SubGraph_InheritContext_True_SharedWriteVisibleInParent`, `SubGraph_InheritContext_False_ParentValuesNotVisible`, `SubGraph_InheritContext_False_ChildWriteNotVisibleInParent`
- [x] T029 [US4] Add `SubGraph_NullTarget_*` and `SubGraph_Nested_*` test groups to `SubGraphTests.cs`: `SubGraph_NullTargetGraph_RaisesOnStuck`, `SubGraph_Nested_DepthGreaterThanOne_Completes`
- [ ] T030 [US4] Run `run_tests` on `SubGraphTests` fixture and confirm all tests pass; fix any failures before proceeding

**Checkpoint**: `SubGraphTests` fully green. Push/pop, both context modes, null guard, and nesting all verified.

---

## Phase 7: User Story 5 — CycleDetection Coverage (Priority: P1)

**Goal**: A single `CycleDetectionTests.cs` fixture covers direct cycles, indirect chains,
valid acyclic graphs, `GraphCycleException.CyclicGraphId`, and the pre-execution guarantee
(no executor ran before the exception).

**Independent Test**: Run `CycleDetectionTests` fixture in isolation → all tests green.

### Implementation

- [x] T031 [P] [US5] Create `Tests/EditMode/Execution/CycleDetectionTests.cs` with namespace `Faolline.GraphCore.Tests`, `List<BaseGraph> _graphs` field, `Track(BaseGraph)` helper, `[TearDown]` destroying all tracked graphs, and inner `LambdaExecutor` stub with call-count closure for pre-execution guarantee test
- [x] T032 [US5] Add `Cycle_Direct_*` test group to `CycleDetectionTests.cs`: `Cycle_Direct_SelfReference_ThrowsGraphCycleException`, `Cycle_Direct_ThrowsBeforeAnyNodeEntered` (executor call count = 1, cyclic re-entry never happens)
- [x] T033 [US5] Add `Cycle_Indirect_*` test group to `CycleDetectionTests.cs`: `Cycle_Indirect_ThreeGraphChain_ThrowsGraphCycleException`, `Cycle_Indirect_ExceptionCarriesOffendingGraphId`
- [x] T034 [US5] Add `Cycle_Valid_*` test group to `CycleDetectionTests.cs`: `Cycle_Valid_AcyclicGraph_CompletesWithoutException`, `Cycle_Valid_NestedSubGraphs_NoException`
- [ ] T035 [US5] Run `run_tests` on `CycleDetectionTests` fixture and confirm all tests pass; fix any failures before proceeding

**Checkpoint**: `CycleDetectionTests` fully green. Direct, indirect, valid, exception payload, and pre-execution guarantee all covered.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Full suite validation and spec completeness check.

- [ ] T036 Run `run_tests` on the full `com.faolline.graphcore.Tests.EditMode` assembly and confirm zero failures, zero errors, zero skipped tests
- [ ] T037 [P] Verify zero `ScriptableObject` leaks: Unity test runner shows no orphaned assets after full suite run (check `read_console` for leak warnings)
- [ ] T038 [P] Verify naming convention: every test method in the five new fixtures follows `MethodName_Scenario_ExpectedResult` (grep `Tests/EditMode/Execution/` for `public void` and audit)
- [ ] T039 [P] Verify `SC-005` from spec: confirm at least one test covers each `RunnerState` value (`Idle` implicitly covered by `Start_*` pre-condition), each `ParameterType` (`Bool`, `Int`, `Float`, `String`), and each `EndReason` used in tests

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — delete legacy files immediately
- **Foundational (Phase 2)**: Compile-check after Phase 1; no blocking task list
- **User Stories (Phases 3–7)**: All depend on Phase 1 cleanup; can run in sequence
  - Phases 3–7 are independent of each other (different files)
- **Polish (Phase 8)**: Depends on all five fixture phases complete

### User Story Dependencies

- **US1 (BaseRunnerTests)**: No dependency on other stories — start after Phase 1
- **US2 (BaseContextTests)**: No dependency on US1 — can start in parallel with US1
- **US3 (HistoryTests)**: No dependency on US1 or US2 — can start in parallel
- **US4 (SubGraphTests)**: No dependency on US1–US3 — can start in parallel
- **US5 (CycleDetectionTests)**: No dependency on US1–US4 — can start in parallel

### Within Each User Story

- Create fixture file first (T005 / T013 / T019 / T025 / T031)
- Write each test group sequentially within the fixture
- Run `run_tests` as the final task of each story to confirm green

### Parallel Opportunities

- T001–T004 (delete old files): all parallel
- T013, T019, T025, T031 (create new fixture files): all parallel — different files
- T036–T039 (polish): T037, T038, T039 parallel with each other

---

## Parallel Example: All Five Fixture Creations

```
Parallel — create empty fixtures with SetUp/TearDown/stubs only:
  T005: Create BaseRunnerTests.cs
  T013: Create BaseContextTests.cs
  T019: Create HistoryTests.cs
  T025: Create SubGraphTests.cs
  T031: Create CycleDetectionTests.cs

Then sequentially per fixture: fill test groups → run_tests → confirm green
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Delete legacy files
2. Confirm compile clean (Phase 2 checkpoint)
3. Complete Phase 3: `BaseRunnerTests` (T005–T012)
4. **STOP and VALIDATE**: `run_tests` on `BaseRunnerTests` only — all green
5. Proceed to remaining stories

### Incremental Delivery

1. Phase 1 cleanup → Phase 3 BaseRunnerTests → green ✅
2. Phase 4 BaseContextTests → green ✅
3. Phase 5 HistoryTests → green ✅
4. Phase 6 SubGraphTests → green ✅
5. Phase 7 CycleDetectionTests → green ✅
6. Phase 8 full suite validation → zero failures ✅

---

## Notes

- `[P]` tasks touch different files — no merge risk when run simultaneously
- Each fixture's final task is always `run_tests` — never skip it
- Do not create a shared `TestStubs.cs`; duplicate the inner stubs per fixture (YAGNI)
- `DataLayer/BaseContextTests.cs` is out of scope — do not touch it
- `Editor/CycleDetectorTests.cs` and `Editor/CycleDetectorIntegrationTests.cs` are out of scope — runtime cycle detection is distinct from editor DFS cycle detection
- The `INodeExecutor.Undo` method should be implemented as a no-op in stubs that don't test undo
