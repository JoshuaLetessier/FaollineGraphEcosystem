---

description: "Task list for GraphCore Execution Runtime"
---

# Tasks: GraphCore Execution Runtime

**Input**: Design documents from `specs/002-execution-runtime/`

**Branch**: `002-execution-runtime`

**Constitution note**: TDD is NON-NEGOTIABLE (Principle IV). For every implementation task,
the corresponding test task MUST be completed first and confirmed failing via `run_tests`
before implementation begins. EditMode tests only — no PlayMode tests required for the runtime.

**Test location**: `Tests/EditMode/Execution/` (new subfolder; DataLayer tests stay in `Tests/EditMode/DataLayer/`)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: Which user story from spec.md (US1–US5)

---

## Phase 1: Setup

**Purpose**: Prepare the directory structure and fix the one existing test that will break
when `BaseContext` is made concrete.

- [X] T001 Update `Tests/EditMode/DataLayer/BaseContextTests.cs` — replace the `BaseContext_IsAbstractClass` test with `BaseContext_IsConcreteClass` (assert `!IsAbstract`); keep `BaseContext_IsNotScriptableObject` and `BaseContext_CanBeSubclassed`
- [X] T002 [P] Create placeholder file `Runtime/Execution/.keep` to establish the directory (removed once first real file is added), OR create the directory via the first `INodeExecutor.cs` file

**Checkpoint**: Existing test suite still green after T001. Directory structure ready.

---

## Phase 2: User Story 2 — BaseContext Blackboard (Priority: P1)

**Goal**: Replace the empty abstract `BaseContext` with a full typed parameter store
with subscriptions, deep clone, and graph initialization.

**Independent Test**: Instantiate `BaseContext`, call `InitFromGraph()` with a 4-parameter
graph, verify each `Get<T>()` returns the correct value, mutate one, verify
`OnParameterChanged` fires, call `DeepClone()`, verify clone has same values but no
subscribers.

> **⚠️ TDD**: Write ALL tests in T003 first. Run `run_tests` to confirm they fail.
> Then implement T004. Then re-run `run_tests` to confirm green.

- [X] T003 [US2] Write failing EditMode tests for `BaseContext` in `Tests/EditMode/Execution/BaseContextTests.cs`:
  - `Set_And_Get_Bool_ReturnsCorrectValue`
  - `Set_And_Get_Int_ReturnsCorrectValue`
  - `Set_And_Get_Float_ReturnsCorrectValue`
  - `Set_And_Get_String_ReturnsCorrectValue`
  - `TryGet_ExistingKey_ReturnsTrueAndValue`
  - `TryGet_MissingKey_ReturnsFalseAndDefault`
  - `Has_ExistingKey_ReturnsTrue`
  - `Has_MissingKey_ReturnsFalse`
  - `Get_MissingKey_ThrowsKeyNotFoundException`
  - `Set_UnsupportedType_ThrowsArgumentException`
  - `OnParameterChanged_FiredOnSet`
  - `OffParameterChanged_NotFiredAfterUnsubscribe`
  - `DeepClone_CopiesValues`
  - `DeepClone_DoesNotCopySubscriptions`
  - `InitFromGraph_PopulatesBoolParameter`
  - `InitFromGraph_PopulatesIntParameter`
  - `InitFromGraph_PopulatesFloatParameter`
  - `InitFromGraph_PopulatesStringParameter`
  - *(Confirm tests FAIL via `run_tests` before proceeding to T004)*

- [X] T004 [US2] Implement `BaseContext` in `Runtime/Graph/BaseContext.cs` — concrete class (not abstract), with `Dictionary<string, object>` parameter store, `Dictionary<string, List<Action<object>>>` subscriber map, `Set<T>`/`Get<T>`/`TryGet<T>`/`Has`, `OnParameterChanged`/`OffParameterChanged`, `InitFromGraph(BaseGraph)`, `virtual DeepClone()` per data-model.md

- [ ] T005 [US2] Confirm all `BaseContextTests` pass via `run_tests`; fix any failures before proceeding

**Checkpoint**: `BaseContext` fully functional as a typed blackboard. All US2 acceptance
scenarios verified. Existing data-layer test suite still green.

---

## Phase 3: User Story 3 — INodeExecutor + NodeExecutorRegistry (Priority: P1)

**Goal**: Deliver the pluggable executor dispatch system so downstream libs can register
type-specific execution logic without modifying graphcore.

**Independent Test**: Register two executors for different `NodeType`s, resolve each by
string, verify correct instance returned. Verify unknown type returns null. Verify duplicate
registration replaces silently.

> **⚠️ TDD**: Write tests in T006 first. Run `run_tests` to confirm failure. Then T007+T008.

- [X] T006 [US3] Write failing EditMode tests for `NodeExecutorRegistry` in `Tests/EditMode/Execution/NodeExecutorRegistryTests.cs`:
  - `GetExecutor_RegisteredType_ReturnsExecutor`
  - `GetExecutor_UnregisteredType_ReturnsNull`
  - `Register_SameTypeTwice_ReplacesFirst`
  - `Register_NullNodeType_ThrowsArgumentNullException`
  - `INodeExecutor_DefaultUndo_IsNoOp` (verify default interface method compiles and does not throw)
  - *(Confirm tests FAIL via `run_tests` before proceeding)*

- [X] T007 [P] [US3] Create `INodeExecutor` interface in `Runtime/Execution/INodeExecutor.cs` — `string NodeType { get; }`, `void Execute(BaseNodeData, BaseContext)`, `void Undo(BaseNodeData, BaseContext) { }` (C# 8 default no-op)

- [X] T008 [P] [US3] Create `NodeExecutorRegistry` in `Runtime/Execution/NodeExecutorRegistry.cs` — `Dictionary<string, INodeExecutor>` backing store, `Register(INodeExecutor)` with null-guard and silent replace, `GetExecutor(string) → INodeExecutor?` returning null for unknowns

- [ ] T009 [US3] Confirm all `NodeExecutorRegistryTests` pass via `run_tests`; fix any failures

**Checkpoint**: Executor registration and resolution fully functional. US3 acceptance
scenarios verified.

---

## Phase 4: User Story 1 — BaseRunner Linear Execution (Priority: P1) 🎯 MVP

**Goal**: Deliver a working headless `BaseRunner` that can drive a linear graph
(Start → Statement → End) from `Start()` to `Ended` state, executing the full
node sequence (EntryConditions → OnEnterActions → Execute → OnNodeCompleted →
OnExitActions → next node).

**Independent Test**: Build a 3-node graph (`StartNodeData` → `StatementNodeData` → `EndNodeData`),
register a stub executor, call `Start()`, auto-call `Proceed()` from `OnNodeCompleted`,
assert runner reaches `Ended` state and correct node sequence was visited.

> **⚠️ TDD**: Write tests in T010 first. Run `run_tests` to confirm failure. Then T011–T014.

- [X] T010 [US1] Write failing EditMode tests for `BaseRunner` linear execution in `Tests/EditMode/Execution/BaseRunnerLinearTests.cs`:
  - `Start_TransitionsToNodeReady_AtEntryNode`
  - `Proceed_ExecutesFullNodeSequence`
  - `Proceed_OnEnterActions_CalledBeforeExecute`
  - `Proceed_OnExitActions_CalledAfterExecute`
  - `Proceed_EntryConditionFails_RaisesOnStuck`
  - `Proceed_ReachesEndNode_TransitionsToEnded`
  - `Proceed_AfterEnded_IsNoOp`
  - `Start_MissingEntryNodeId_ThrowsInvalidOperationException`
  - `OnNodeCompleted_RaisedAfterExecute`
  - `OnEnded_RaisedWithCorrectReason`
  - *(Confirm tests FAIL via `run_tests` before proceeding)*

- [X] T011 [P] [US1] Create `RunnerState` enum in `Runtime/Execution/RunnerState.cs` — values `Idle=0`, `NodeReady=1`, `Paused=2`, `Ended=3`

- [X] T012 [P] [US1] Create `GraphExecutionState` class in `Runtime/Execution/GraphExecutionState.cs` — fields: `BaseGraph Graph`, `string CurrentNodeId`, `List<BaseEdgeData> AvailableEdges`; shallow-clone method per data-model.md

- [X] T013 [P] [US1] Create `HistoryEntry` class stub in `Runtime/Execution/HistoryEntry.cs` — fields: `string NodeId`, `Stack<GraphExecutionState> GraphStackSnapshot`, `BaseContext ContextSnapshot` (full history logic implemented in Phase 6)

- [X] T014 [US1] Implement `BaseRunner` (linear path only — no SubGraph stack, no history writes) in `Runtime/Execution/BaseRunner.cs`:
  - State machine with `RunnerState` backing field
  - `Stack<GraphExecutionState> _graphStack` (single frame for linear)
  - Events: `OnNodeEntered`, `OnNodeCompleted`, `OnEnded`, `OnStuck` (all `C# Action<T>`)
  - `Start(BaseGraph, BaseContext, NodeExecutorRegistry)` — validates EntryNodeId, transitions to `NodeReady`
  - Node execution sequence: EntryConditions → OnEnterActions → Execute → raise `OnNodeCompleted`
  - `Proceed()` — runs OnExitActions, evaluates outgoing edges, advances or ends
  - `ChooseById(string)` — selects edge/choice by id
  - SubGraph/history stubs (no-ops for now): `GoBack()`, `GoBackToCheckpoint()`

- [ ] T015 [US1] Confirm all `BaseRunnerLinearTests` pass via `run_tests`; fix any failures

**Checkpoint**: MVP achieved. A complete linear graph executes end-to-end. Validate against
spec US1 acceptance scenarios before proceeding.

---

## Phase 5: User Story 4 — SubGraph Navigation + Cycle Detection (Priority: P2)

**Goal**: Enable `BaseRunner` to push/pop sub-graph stack frames when encountering
`SubGraphNodeData`, and raise `GraphCycleException` on any detected cycle.

**Independent Test**: Build parent + child graphs, drive runner through SubGraph entry,
verify stack depth increases, drive to End in sub-graph, verify pop back to parent.
Build a cyclic reference and verify `GraphCycleException` is thrown.

> **⚠️ TDD**: Write tests in T016 first. Run `run_tests` to confirm failure. Then T017–T019.

- [X] T016 [US4] Write failing EditMode tests for SubGraph navigation in `Tests/EditMode/Execution/BaseRunnerSubGraphTests.cs`:
  - `SubGraphNode_PushesGraphStack`
  - `SubGraphEnd_PopsGraphStack_ResumesParent`
  - `SubGraph_InheritParentContext_True_SharesContext`
  - `SubGraph_InheritParentContext_False_GetsFreshContext`
  - `CycleDetection_ThrowsGraphCycleException`
  - `GraphCycleException_CarriesOffendingGraphId`
  - `NestedSubGraph_DepthGreaterThanOne_Works`
  - *(Confirm tests FAIL via `run_tests` before proceeding)*

- [X] T017 [P] [US4] Create `GraphCycleException` in `Runtime/Execution/GraphCycleException.cs` — `sealed`, extends `Exception`, `string CyclicGraphId` property, message format: `"[GraphCore] Cycle detected: graph '{graphId}' is already in the execution stack."`

- [X] T018 [US4] Extend `BaseRunner` in `Runtime/Execution/BaseRunner.cs` with SubGraph support:
  - On `SubGraphNodeData` entry: check `_graphStack` for `targetGraph.GraphId` → throw `GraphCycleException` if found
  - Push new `GraphExecutionState` frame for target graph
  - Context forwarding: `InheritParentContext = true` → pass `_context`; `false` → `new BaseContext()` + `InitFromGraph(targetGraph)`
  - On `EndNode` with stack depth > 1: pop frame, resume parent at next node via `Proceed()`

- [ ] T019 [US4] Confirm all `BaseRunnerSubGraphTests` pass via `run_tests`; fix any failures

**Checkpoint**: SubGraph nesting and cycle detection verified. US4 acceptance scenarios
validated.

---

## Phase 6: User Story 5 — History Rewind (Priority: P3)

**Goal**: Enable `GoBack()` (one step) and `GoBackToCheckpoint()` (nearest `IsCheckpoint` node)
with the history stack capped by `BaseGraph.HistoryDepth`.

**Independent Test**: Execute a 5-node graph with one checkpoint at node 3. From node 5,
call `GoBack()` twice and verify arrival at node 3 with restored context. Call
`GoBackToCheckpoint()` directly from node 5 and verify same result. Verify cap enforcement
with `HistoryDepth = 3`.

> **⚠️ TDD**: Write tests in T020 first. Run `run_tests` to confirm failure. Then T021–T022.

- [X] T020 [US5] Write failing EditMode tests for history in `Tests/EditMode/Execution/BaseRunnerHistoryTests.cs`:
  - `GoBack_RestoresPreviousNodeAndContext`
  - `GoBack_EmptyHistory_IsNoOp`
  - `GoBackToCheckpoint_RestoresNearestCheckpointNode`
  - `GoBackToCheckpoint_NoCheckpointInHistory_IsNoOp`
  - `History_CappedByHistoryDepth`
  - `History_DepthZero_Unlimited`
  - `GoBack_CrossSubGraphBoundary_RestoresFullStack`
  - *(Confirm tests FAIL via `run_tests` before proceeding)*

- [X] T021 [US5] Complete `HistoryEntry` and integrate history snapshots into `BaseRunner`:
  - After `OnExitActions` and before advancing: append `HistoryEntry { NodeId, GraphStack clone, Context.DeepClone() }` to `_history`
  - Trim `_history` from front when `HistoryDepth > 0` and `Count > HistoryDepth`
  - `GoBack()`: if `_history` empty → no-op; else call `Undo` on current executor, pop last entry, restore `_graphStack` and `_context` parameter values, set `State = NodeReady`
  - `GoBackToCheckpoint()`: scan `_history` from newest to oldest for entry whose node has `IsCheckpoint = true`; if found, truncate and apply; else no-op

- [ ] T022 [US5] Confirm all `BaseRunnerHistoryTests` pass via `run_tests`; fix any failures

**Checkpoint**: Full history rewind (GoBack + GoBackToCheckpoint + cap enforcement) verified.
US5 acceptance scenarios validated.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, version bump, and final validation.

- [X] T023 [P] Add XML `<summary>` documentation to all public members of `BaseContext`, `INodeExecutor`, `NodeExecutorRegistry`, `BaseRunner`, `RunnerState`, `GraphExecutionState`, `HistoryEntry`, `GraphCycleException`
- [X] T024 [P] Update `package.json` version from `0.1.0` to `0.2.0` (MINOR bump per semver assessment in contracts/public-api.md)
- [ ] T025 Run full EditMode suite via `run_tests` — verify zero errors, zero warnings; add inline comments for any justified warning suppressions per constitution
- [ ] T026 Run `validate_script` on all modified and new `.cs` files via Coplay MCP
- [ ] T027 Run `unity_reflect` to verify all Unity APIs used in new files are available in Unity 6000.x
- [ ] T028 Run `manage_packages` to confirm all asmdef references resolve correctly
- [ ] T029 Validate quickstart.md scenarios manually: execute each code example against the implemented API, confirm compile and runtime correctness

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (US2 — BaseContext)**: Depends on Phase 1 — BLOCKS all subsequent phases
- **Phase 3 (US3 — INodeExecutor/Registry)**: Depends on Phase 2 — BLOCKS Phase 4
- **Phase 4 (US1 — BaseRunner Linear)**: Depends on Phases 2 + 3 — MVP deliverable
- **Phase 5 (US4 — SubGraph)**: Depends on Phase 4
- **Phase 6 (US5 — History)**: Depends on Phase 4 (and Phase 5 for cross-SubGraph rewind)
- **Phase 7 (Polish)**: Depends on all story phases

### Within Each Phase

1. Test task FIRST — confirm it fails via `run_tests`
2. Implementation tasks (parallel where marked `[P]`)
3. Confirm test task — all green before proceeding to next phase

### Parallel Opportunities

- T007 (`INodeExecutor.cs`) and T008 (`NodeExecutorRegistry.cs`) — different files
- T011 (`RunnerState.cs`), T012 (`GraphExecutionState.cs`), T013 (`HistoryEntry.cs`) — different files, all in same phase
- T017 (`GraphCycleException.cs`) and T018 (`BaseRunner` extension) — different files
- T023 (docs) and T024 (package.json) — different files, no dependencies

---

## Parallel Example: Phase 4 (US1 — BaseRunner Setup)

```
# After T010 (tests written and confirmed failing):

Parallel batch:
  Task T011: Create RunnerState.cs
  Task T012: Create GraphExecutionState.cs
  Task T013: Create HistoryEntry.cs (stub)

Then sequential:
  Task T014: Implement BaseRunner.cs (depends on T011, T012, T013)
  Task T015: Confirm tests pass
```

---

## Implementation Strategy

### MVP First (Phases 1–4 only)

1. Complete Phase 1: Setup
2. Complete Phase 2: BaseContext (US2) — blackboard working
3. Complete Phase 3: INodeExecutor + Registry (US3) — dispatch working
4. Complete Phase 4: BaseRunner linear (US1) — **MVP: full graph execution**
5. **STOP and VALIDATE**: Run `run_tests`, demo with a 3-node linear graph

### Incremental Delivery

1. Phases 1–4 → MVP (linear execution end-to-end)
2. Phase 5 → Add SubGraph support + cycle detection
3. Phase 6 → Add history rewind + checkpoint
4. Phase 7 → Polish and ship

---

## Notes

- `[P]` = different files, safe to implement in parallel within same phase
- `[USN]` maps each task to a user story for traceability
- TDD is non-negotiable per constitution: test → fail → implement → pass
- No PlayMode tests — all tests are EditMode (constitution Principle IV)
- `BaseContext_IsAbstractClass` in the existing `DataLayer/BaseContextTests.cs` MUST be updated (T001) before BaseContext is made concrete, or the existing suite will fail
- History snapshots use `BaseContext.DeepClone()` — subclass contexts must override `DeepClone()` to preserve their additional fields
