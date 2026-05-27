---
description: "Task list for GraphCore Data Layer"
---

# Tasks: GraphCore Data Layer

**Input**: Design documents from `specs/001-data-layer/`

**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅

**TDD Mandate**: Constitution Principle IV is NON-NEGOTIABLE. Every implementation task
MUST be preceded by a failing test confirmed via Coplay MCP `run_tests`. Checkpoints
marked ⚠️ GATE and ✅ CONFIRM enforce the Red-Green cycle.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency)
- **[Story]**: User story this task belongs to (US1–US4)
- All file paths are relative to the repository root

## Path Conventions

```
Runtime/                             ← Runtime assembly root
Tests/EditMode/DataLayer/            ← EditMode test files root
```

---

## Phase 1: Setup

**Purpose**: UPM package scaffold — required before any C# file can be written.

- [x] T001 Create package.json at package.json with `"name": "com.faolline.graphcore"`, `"unity": "6000.0"`, `"version": "0.1.0"`
- [x] T002 [P] Create Runtime/com.faolline.graphcore.Runtime.asmdef (no references; platforms: Any)
- [x] T003 [P] Create Tests/EditMode/com.faolline.graphcore.Tests.EditMode.asmdef referencing com.faolline.graphcore.Runtime, UnityEngine.TestRunner, UnityEditor.TestRunner; testPlatforms: EditMode
- [x] T004 Create Runtime subdirectories: Runtime/Graph/ Runtime/Nodes/ Runtime/Edges/ Runtime/Choices/ Runtime/Parameters/ Runtime/Actions/ Runtime/Conditions/ Tests/EditMode/DataLayer/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All types that `BaseGraph` depends on to compile. MUST be complete before
any user story implementation can begin.

**⚠️ CRITICAL**: No user story implementation can begin until this phase is complete.

### Tests for Foundational Types ⚠️ WRITE FIRST — MUST FAIL BEFORE IMPLEMENTING

- [x] T005 [P] Write failing test: `typeof(BaseContext).IsAbstract == true` in Tests/EditMode/DataLayer/BaseContextTests.cs
- [x] T006 [P] Write failing test: `BaseAction` has abstract `Execute(BaseContext)` method signature in Tests/EditMode/DataLayer/BaseActionTests.cs
- [x] T007 [P] Write failing test: `BaseCondition` has abstract `Evaluate(BaseContext)` returning `bool` in Tests/EditMode/DataLayer/BaseConditionTests.cs
- [x] T008 [P] Write failing test: `BaseChoice` serializes `Id` (string) and `Condition` (null by default) in Tests/EditMode/DataLayer/BaseChoiceTests.cs
- [x] T009 [P] Write failing test: `ParameterType` enum has exactly Bool(0) Int(1) Float(2) String(3) in Tests/EditMode/DataLayer/ParameterDataTests.cs
- [x] T010 [P] Write failing test: `EndReason` enum has exactly Completed(0) Cancelled(1) Error(2) in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [x] T011 [P] Write failing test: `ParameterData` Key/Type/DefaultValue round-trip in Tests/EditMode/DataLayer/ParameterDataTests.cs
- [x] T012 [P] Write failing test: `BaseNodeData` has Id/NodeType/Position/SerializedPayload accessible fields in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [x] T013 [P] Write failing test: `BaseEdgeData` serializes Id/FromNodeId/ToNodeId/PortName/HasColorOverride in Tests/EditMode/DataLayer/BaseEdgeDataTests.cs
- [ ] T014 Confirm T005–T013 ALL FAIL via Coplay MCP `run_tests` ⚠️ GATE — **MANUAL: run in Unity 6 before T015**

### Implementation for Foundational Types

- [x] T015 [P] Implement `BaseContext` abstract class (no fields) in Runtime/Graph/BaseContext.cs
- [x] T016 [P] Implement `BaseAction` abstract ScriptableObject with `abstract void Execute(BaseContext context)` in Runtime/Actions/BaseAction.cs
- [x] T017 [P] Implement `BaseCondition` abstract ScriptableObject with `abstract bool Evaluate(BaseContext context)` in Runtime/Conditions/BaseCondition.cs
- [x] T018 [P] Implement `ParameterType` enum (Bool=0, Int=1, Float=2, String=3) in Runtime/Parameters/ParameterType.cs
- [x] T019 [P] Implement `EndReason` enum (Completed=0, Cancelled=1, Error=2) in Runtime/Nodes/EndReason.cs
- [x] T020 Implement `BaseChoice` [Serializable] with `Id` (string) and `Condition` (BaseCondition, nullable) in Runtime/Choices/BaseChoice.cs (depends on T017)
- [x] T021 Implement `ParameterData` [Serializable] with Key/Type/DefaultValue in Runtime/Parameters/ParameterData.cs (depends on T018)
- [x] T022 Implement `BaseNodeData` abstract [Serializable] with all fields per data-model.md — Id, NodeType, Position, SerializedPayload, EntryConditions, OnEnterActions, OnExitActions (init to new List<T>()), IsCheckpoint, HasColorOverride, NodeColor in Runtime/Nodes/BaseNodeData.cs (depends on T015 T016 T017)
- [x] T023 Implement `BaseEdgeData` [Serializable] with Id/FromNodeId/ToNodeId/PortName/Condition/HasColorOverride/EdgeColor in Runtime/Edges/BaseEdgeData.cs (depends on T017)
- [ ] T024 Confirm T005–T013 ALL PASS via Coplay MCP `run_tests` ✅ CONFIRM — **MANUAL: run in Unity 6**

---

## Phase 3: User Story 1 — Define and Persist a Graph Asset (Priority: P1) 🎯 MVP

**Goal**: `BaseGraph` ScriptableObject serializes and restores all data across Unity reloads.

**Independent Test**: Create a `BaseGraph` asset in EditMode, set `EntryNodeId`, save,
reload domain, assert `GraphId` is unchanged, `HistoryDepth` is 20, lists are non-null.

### Tests for User Story 1 ⚠️ WRITE FIRST — MUST FAIL BEFORE IMPLEMENTING

- [x] T025 [P] [US1] Write failing test: `BaseGraph.GraphId` is non-empty after `OnEnable` in Tests/EditMode/DataLayer/BaseGraphTests.cs
- [x] T026 [P] [US1] Write failing test: `BaseGraph.GraphId` is not reassigned on second `OnEnable` call in Tests/EditMode/DataLayer/BaseGraphTests.cs
- [x] T027 [P] [US1] Write failing test: `BaseGraph.HistoryDepth` defaults to 20 in Tests/EditMode/DataLayer/BaseGraphTests.cs
- [x] T028 [P] [US1] Write failing test: `BaseGraph.Nodes`, `Edges`, `Parameters` are non-null on new instance in Tests/EditMode/DataLayer/BaseGraphTests.cs
- [ ] T029 [US1] Confirm T025–T028 ALL FAIL via Coplay MCP `run_tests` ⚠️ GATE — **MANUAL: run in Unity 6 before T030**

### Implementation for User Story 1

- [x] T030 [US1] Implement `BaseGraph` ScriptableObject per data-model.md: `[SerializeField] private string _graphId`, `OnEnable` GUID guard, `[SerializeReference] List<BaseNodeData>`, `[SerializeReference] List<BaseEdgeData>`, `List<ParameterData>`, `EntryNodeId`, `HistoryDepth = 20` in Runtime/Graph/BaseGraph.cs (depends on T020 T021 T022 T023)
- [ ] T031 [US1] Confirm T025–T028 ALL PASS via Coplay MCP `run_tests` ✅ CONFIRM — **MANUAL: run in Unity 6**

**Checkpoint**: `BaseGraph` fully serializable. User Story 1 independently validated.

---

## Phase 4: User Story 2 — Nodes Carry Universal Lifecycle Hooks (Priority: P1)

**Goal**: Every node type, regardless of subclass, has non-null `EntryConditions`,
`OnEnterActions`, and `OnExitActions` lists that survive serialization through `BaseGraph`.

**Independent Test**: Instantiate a concrete `BaseNodeData` subclass, add to a `BaseGraph`,
assert all three hook lists are non-null and independently modifiable.

### Tests for User Story 2 ⚠️ WRITE FIRST — MUST FAIL BEFORE IMPLEMENTING

- [x] T032 [P] [US2] Write failing test: `BaseNodeData` subclass `EntryConditions` is non-null on construction in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [x] T033 [P] [US2] Write failing test: `BaseNodeData` subclass `OnEnterActions` is non-null on construction in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [x] T034 [P] [US2] Write failing test: `BaseNodeData` subclass `OnExitActions` is non-null on construction in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [x] T035 [P] [US2] Write failing test: `HasColorOverride = true` and `NodeColor` round-trip through `BaseGraph` serialization in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [x] T036 [P] [US2] Write failing test: `IsCheckpoint = true` persists through `BaseGraph` serialization in Tests/EditMode/DataLayer/BaseNodeDataTests.cs
- [ ] T037 [US2] Confirm T032–T036 ALL FAIL via Coplay MCP `run_tests` ⚠️ GATE — **MANUAL: run in Unity 6 before T038**

### Implementation for User Story 2

- [x] T038 [US2] Verify `BaseNodeData` in Runtime/Nodes/BaseNodeData.cs initializes `EntryConditions`, `OnEnterActions`, `OnExitActions` each to `new List<T>()` in the default constructor — amend if missing
- [ ] T039 [US2] Confirm T032–T036 ALL PASS via Coplay MCP `run_tests` ✅ CONFIRM — **MANUAL: run in Unity 6**

**Checkpoint**: All nodes carry lifecycle hooks. User Story 2 independently validated.

---

## Phase 5: User Story 3 — Built-in Node Types (Priority: P2)

**Goal**: Five concrete built-in node types cover all graph primitives, each with a
`const string NodeTypeId` and their unique data fields. All are subclassable.

**Independent Test**: Instantiate each type, verify `NodeTypeId` const value, verify
unique fields are accessible and serialize correctly through `BaseGraph.Nodes`.

### Tests for User Story 3 ⚠️ WRITE FIRST — MUST FAIL BEFORE IMPLEMENTING

- [x] T040 [P] [US3] Write failing test: `StartNodeData.NodeTypeId == "graphcore/start"` and inherits `BaseNodeData` in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [x] T041 [P] [US3] Write failing test: `StatementNodeData.NodeTypeId == "graphcore/statement"` and inherits `BaseNodeData` in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [x] T042 [P] [US3] Write failing test: `ChoiceNodeData.NodeTypeId == "graphcore/choice"` and `Choices` list is non-null on construction in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [x] T043 [P] [US3] Write failing test: `EndNodeData.NodeTypeId == "graphcore/end"` and `EndReason` defaults to `Completed` in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [x] T044 [P] [US3] Write failing test: `SubGraphNodeData.NodeTypeId == "graphcore/subgraph"` and has `TargetGraph` (nullable) and `InheritParentContext` fields in Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs
- [ ] T045 [US3] Confirm T040–T044 ALL FAIL via Coplay MCP `run_tests` ⚠️ GATE — **MANUAL: run in Unity 6 before T046**

### Implementation for User Story 3

- [x] T046 [P] [US3] Implement `StartNodeData` with `public const string NodeTypeId = "graphcore/start"` in Runtime/Nodes/StartNodeData.cs (depends on T022)
- [x] T047 [P] [US3] Implement `StatementNodeData` with `public const string NodeTypeId = "graphcore/statement"` in Runtime/Nodes/StatementNodeData.cs (depends on T022)
- [x] T048 [US3] Implement `ChoiceNodeData` with `public const string NodeTypeId = "graphcore/choice"` and `[SerializeReference] List<BaseChoice> Choices` (init to new List) in Runtime/Nodes/ChoiceNodeData.cs (depends on T022 T020)
- [x] T049 [P] [US3] Implement `EndNodeData` with `public const string NodeTypeId = "graphcore/end"` and `EndReason EndReason = EndReason.Completed` in Runtime/Nodes/EndNodeData.cs (depends on T022 T019)
- [x] T050 [US3] Implement `SubGraphNodeData` with `public const string NodeTypeId = "graphcore/subgraph"`, `BaseGraph TargetGraph`, `bool InheritParentContext` in Runtime/Nodes/SubGraphNodeData.cs (depends on T022 T030)
- [ ] T051 [US3] Confirm T040–T044 ALL PASS via Coplay MCP `run_tests` ✅ CONFIRM — **MANUAL: run in Unity 6**

**Checkpoint**: All five built-in types functional. User Story 3 independently validated.

---

## Phase 6: User Story 4 — Parameters Enable Graph-Level State (Priority: P2)

**Goal**: `ParameterData` entries on a `BaseGraph` carry typed variables that survive
save/reload with no data loss.

**Independent Test**: Add four `ParameterData` entries (one per type) to a `BaseGraph`,
save and reload, assert all keys, types, and default values are preserved exactly.

### Tests for User Story 4 ⚠️ WRITE FIRST — MUST FAIL BEFORE IMPLEMENTING

- [x] T052 [P] [US4] Write failing test: `ParameterData` with `Type = Bool` round-trips `Key` and `DefaultValue` through `BaseGraph` serialization in Tests/EditMode/DataLayer/ParameterDataTests.cs
- [x] T053 [P] [US4] Write failing test: all four `ParameterType` values (`Bool`, `Int`, `Float`, `String`) serialize distinctly in a `List<ParameterData>` in Tests/EditMode/DataLayer/ParameterDataTests.cs
- [ ] T054 [US4] Confirm T052–T053 ALL FAIL via Coplay MCP `run_tests` ⚠️ GATE — **MANUAL: run in Unity 6 before T055**

### Implementation for User Story 4

- [x] T055 [US4] Verify `ParameterData` in Runtime/Parameters/ParameterData.cs correctly serializes all three fields — amend if any field has wrong access modifier or missing `[SerializeField]`
- [ ] T056 [US4] Confirm T052–T053 ALL PASS via Coplay MCP `run_tests` ✅ CONFIRM — **MANUAL: run in Unity 6**

**Checkpoint**: Parameters fully functional. All four user stories independently validated.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, static analysis, and pre-merge gate (per constitution).

- [x] T057 [P] Add XML `<summary>` documentation to all public types and public members in Runtime/ (all 15 files)
- [ ] T058 [P] Run Coplay MCP `validate_script` on all Runtime/ files — resolve until zero errors — **MANUAL**
- [ ] T059 [P] Run Coplay MCP `unity_reflect` — verify all ScriptableObject/Unity 6 APIs exist and are non-deprecated — **MANUAL**
- [ ] T060 [P] Run Coplay MCP `manage_packages` — verify com.faolline.graphcore.Runtime and Tests asmdefs resolve all references — **MANUAL**
- [ ] T061 Run full EditMode suite via Coplay MCP `run_tests` — all test assertions green — **MANUAL**
- [ ] T062 Run Coplay MCP `read_console` — zero errors; add inline `// [GraphCore]` justified comment for any remaining warnings — **MANUAL**
- [x] T063 Verify zero references to ecosystem libs in Runtime/ — grep for `dialoguesystem`, `gameflow`, `questsystem`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 completion
- **US2 (Phase 4)**: Depends on Phase 2 completion (can start after Phase 2; parallel with Phase 3 if Phase 2 is done)
- **US3 (Phase 5)**: Depends on Phase 3 completion (`SubGraphNodeData` needs `BaseGraph`)
- **US4 (Phase 6)**: Depends on Phase 2 completion (can start after Phase 2; parallel with Phase 3/4 if resources allow)
- **Polish (Phase 7)**: Depends on all user story phases complete

### User Story Dependencies

- **US1 (P1)**: Unblocked after Phase 2
- **US2 (P1)**: Unblocked after Phase 2 — independent of US1
- **US3 (P2)**: Requires US1 complete (SubGraphNodeData references BaseGraph from T030)
- **US4 (P2)**: Unblocked after Phase 2 — independent of US1/US2

### Within Each Phase: TDD Order

1. Write ALL tests for the phase first (parallel where marked [P])
2. Run `run_tests` — CONFIRM ALL FAIL (⚠️ GATE task)
3. Implement (parallel where marked [P], respecting internal depends-on order)
4. Run `run_tests` — CONFIRM ALL PASS (✅ CONFIRM task)

### Key Internal Dependencies (within Phase 2)

```
T015 (BaseContext)
  ↓
T016 (BaseAction)   T017 (BaseCondition)
                      ↓
                    T020 (BaseChoice)
T018 (ParameterType)
  ↓
T021 (ParameterData)
T019 (EndReason) — standalone
T015 + T016 + T017 → T022 (BaseNodeData)
T017 → T023 (BaseEdgeData)
```

---

## Parallel Example: Phase 2 Test Writing

```
# All T005–T013 can be written in parallel (different files):
T005 → Tests/EditMode/DataLayer/BaseContextTests.cs
T006 → Tests/EditMode/DataLayer/BaseActionTests.cs
T007 → Tests/EditMode/DataLayer/BaseConditionTests.cs
T008 → Tests/EditMode/DataLayer/BaseChoiceTests.cs
T009 → Tests/EditMode/DataLayer/ParameterDataTests.cs       (shared with T011, T052, T053)
T010 → Tests/EditMode/DataLayer/BuiltInNodeTypesTests.cs    (shared with T040–T044)
T012 → Tests/EditMode/DataLayer/BaseNodeDataTests.cs        (shared with T032–T036)
T013 → Tests/EditMode/DataLayer/BaseEdgeDataTests.cs
```

## Parallel Example: Phase 5 (US3) Implementation

```
# T046, T047, T049 have no inter-dependencies — run in parallel:
T046 → Runtime/Nodes/StartNodeData.cs
T047 → Runtime/Nodes/StatementNodeData.cs
T049 → Runtime/Nodes/EndNodeData.cs

# T048 and T050 have dependencies:
T048 → Runtime/Nodes/ChoiceNodeData.cs  (after T020 BaseChoice)
T050 → Runtime/Nodes/SubGraphNodeData.cs (after T030 BaseGraph)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational — all tests written, fail confirmed, implementations done, pass confirmed
3. Complete Phase 3: User Story 1 (BaseGraph)
4. **STOP and VALIDATE**: All US1 acceptance scenarios verified
5. Feature is usable: a `BaseGraph` asset can be created, populated, and saved

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. Phase 3 (US1) → `BaseGraph` asset usable ← **MVP**
3. Phase 4 (US2) → Lifecycle hooks validated on any node
4. Phase 5 (US3) → All 5 built-in types available
5. Phase 6 (US4) → Typed parameters functional
6. Phase 7 → Pre-merge quality gates clear

---

## Notes

- `[P]` tasks write to different files — safe to parallelize
- `[Story]` label maps each task to its user story for traceability
- TDD GATE tasks (⚠️) must not be skipped — if tests don't fail, the test is not testing the right thing; investigate before implementing
- TDD CONFIRM tasks (✅) must pass before moving to the next phase
- All `run_tests` calls go through Coplay MCP, not direct CLI, per constitution Principle IV
- `[GraphCore]` prefix MUST appear on all `Debug.LogError` calls
- Commit after each confirmed-pass checkpoint (✅ CONFIRM tasks)
