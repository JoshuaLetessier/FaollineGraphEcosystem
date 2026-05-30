---
description: "Task list for starterGraph — Reusable Downstream-Lib Starter"
---

# Tasks: starterGraph — Reusable Downstream-Lib Starter

**Input**: Design documents from `specs/009-starter-graph/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: INCLUDED — Constitution Principle IV mandates TDD (Red-Green-Refactor). Test tasks are authored first and must FAIL before implementation.

**Organization**: Grouped by user story (US1 runtime → US2 editor → US3 robustness). US1 is the MVP and a prerequisite for US2/US3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no incomplete dependency)
- **[Story]**: US1 / US2 / US3
- Paths are relative to the new package root `com.faolline.starterGraph/`

## Path Conventions

New package `com.faolline.starterGraph` with three assemblies (Runtime, Editor, Tests.EditMode), mirroring the validated `com.faolline.graphTest`. Editor types extend graphcore's `BaseGraphView`/`BaseNodeView`/`BaseEdgeView`/`BaseNodeInspectorView`/`BaseGraphEditorWindow`. No graphcore changes.

---

## Phase 1: Setup (package skeleton)

- [x] T001 Create the package folder tree and `com.faolline.starterGraph/package.json` (name, version, dependency on graphcore) per plan.md
- [x] T002 [P] Create `com.faolline.starterGraph/Runtime/com.faolline.starterGraph.Runtime.asmdef` referencing `com.faolline.graphcore.Runtime`
- [x] T003 [P] Create `com.faolline.starterGraph/Editor/com.faolline.starterGraph.Editor.asmdef` (Editor platform) referencing graphcore Runtime+Editor and `com.faolline.starterGraph.Runtime`
- [x] T004 [P] Create `com.faolline.starterGraph/Tests/EditMode/com.faolline.starterGraph.Tests.EditMode.asmdef` referencing the test framework + starter Runtime/Editor + graphcore

---

## Phase 2: Foundational

**Purpose**: None beyond Setup — US1 is the runtime foundation that US2/US3 build on, handled as the P1 story. No other shared infrastructure blocks the stories.

**Checkpoint**: Proceed to US1.

---

## Phase 3: User Story 1 — Runtime foundation (Priority: P1) 🎯 MVP

**Goal**: `StarterGraph`, the typed `StarterContext`/`Keys` (bool/int/float/string + clone), `StarterChoice`, the typed conditions and actions, and an example statement node — usable to author and run graphs in code.

**Independent Test**: In code, build Start → Statement(set int) → edge gated by an int condition → End, run via `BaseRunner`, confirm the branch is taken; clone the context and confirm the subtype + values; confirm a typed value survives GoBack.

### Tests for User Story 1 ⚠️ (write first, must FAIL)

- [x] T005 [P] [US1] Write failing test: `StarterGraph` is a `BaseGraph` with `[CreateAssetMenu]`, holds nodes/edges/parameters — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterGraphTests.cs`
- [x] T006 [P] [US1] Write failing test: `StarterContext` typed properties (bool/int/float/string) round-trip; `DeepClone()` returns a `StarterContext` with values; `CreateCloneInstance()` returns the subtype — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterContextTests.cs`
- [x] T007 [P] [US1] Write failing test: `StarterChoice` derives from `BaseChoice`, is `[Serializable]`, carries a `Label` — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterChoiceTests.cs`
- [x] T008 [P] [US1] Write failing tests: all conditions (always-true/false, bool, int+operator, float+operator, string eq/negate) evaluate correctly and return false+warning (never throw) on a missing/mistyped key — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterConditionTests.cs`
- [x] T009 [P] [US1] Write failing tests: all actions (log, set bool/int/float/string) write the correct typed value into the context — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterActionTests.cs`
- [x] T010 [P] [US1] Write failing test: running a graph that sets int/float/string then GoBack restores the typed context values (snapshot/restore) — in `com.faolline.starterGraph/Tests/EditMode/Runtime/StarterContextHistoryTests.cs`

### Implementation for User Story 1

- [x] T011 [P] [US1] Implement `StarterGraph : BaseGraph` with `[CreateAssetMenu]` in `com.faolline.starterGraph/Runtime/StarterGraph.cs`
- [x] T012 [P] [US1] Implement `StarterContextKeys` (one `const string` per key) in `com.faolline.starterGraph/Runtime/StarterContextKeys.cs`
- [x] T013 [US1] Implement `StarterContext : BaseContext` with typed `Flag`(bool)/`Score`(int)/`Ratio`(float)/`Label`(string) properties via `StarterContextKeys`, and `CreateCloneInstance()` override, in `com.faolline.starterGraph/Runtime/StarterContext.cs`
- [x] T014 [P] [US1] Implement `StarterChoice : BaseChoice` (+ `Label`) in `com.faolline.starterGraph/Runtime/Choices/StarterChoice.cs`
- [x] T015 [P] [US1] Implement `StarterStatementNodeData : StatementNodeData` (+ editable `Label`, `NodeTypeId`) in `com.faolline.starterGraph/Runtime/Nodes/StarterStatementNodeData.cs`
- [x] T016 [P] [US1] Implement `ComparisonOperator` enum in `com.faolline.starterGraph/Runtime/Conditions/ComparisonOperator.cs`
- [x] T017 [US1] Implement the conditions (`StarterAlwaysTrueCondition`, `StarterAlwaysFalseCondition`, `StarterBoolCondition`, `StarterIntCondition`, `StarterFloatCondition`, `StarterStringCondition`; null-safe; `[CreateAssetMenu]`) under `com.faolline.starterGraph/Runtime/Conditions/`
- [x] T018 [P] [US1] Implement the actions (`StarterLogAction`, `StarterSetBoolAction`, `StarterSetIntAction`, `StarterSetFloatAction`, `StarterSetStringAction`; `[CreateAssetMenu]`) under `com.faolline.starterGraph/Runtime/Actions/`

**Checkpoint**: Runtime socle compiles and its tests pass — a lib can build headless logic on it (SC-002, SC-003).

---

## Phase 4: User Story 2 — Full editor (Priority: P2)

**Goal**: Author every node type on the canvas, edit each in the inspector, and run/navigate from the editor window.

**Independent Test**: Add one of each node type, configure Choice/SubGraph/EndReason/typed params in the inspector, Run → pause at Choice → Choose → resume; GoBack/Continue work.

### Tests for User Story 2 ⚠️ (write first, must FAIL)

- [x] T019 [P] [US2] Write failing tests for the node views: Start (`out`), End (`in`), Statement (`in`+`out`), Choice (one `in`, one output per choice with `portName == choice.Id`), SubGraph (`in`+`out`) — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterNodeViewTests.cs`
- [x] T020 [P] [US2] Write failing test: `StarterGraphView.CreateNodeView` dispatches each node type and "Add … Node" menu entries create them — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterGraphViewTests.cs`
- [x] T021 [P] [US2] Write failing tests for the inspector sections: label (Statement), EndReason set, choice add/remove + label/condition, sub-graph target/inherit + cycle refusal, typed parameter add/remove (bool/int/float/string) — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterInspectorTests.cs`
- [x] T022 [P] [US2] Write failing tests for the window execution: Run pauses at a Choice (waiting flag), `Choose` routes by id and resumes, `Continue`/`GoBack` navigate, end reason logged — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterWindowExecutionTests.cs`

### Implementation for User Story 2

- [x] T023 [P] [US2] Implement `StarterEdgeView : BaseEdgeView` in `com.faolline.starterGraph/Editor/Edges/StarterEdgeView.cs`
- [x] T024 [US2] Implement the five node views (`StartNodeView`, `EndNodeView`, `StarterStatementNodeView`, `ChoiceNodeView` with dynamic id-routed output ports + `RebuildPorts`/`UpdateChoiceLabel`, `SubGraphNodeView`) under `com.faolline.starterGraph/Editor/Nodes/`
- [x] T025 [US2] Implement `StarterGraphView : BaseGraphView` — `CreateNodeView` dispatch for all node types + `StarterEdgeView`, context menu for each node type, `GetChoiceView`/`RemoveChoiceEdges` — in `com.faolline.starterGraph/Editor/Graph/StarterGraphView.cs`
- [x] T026 [US2] Implement `StarterNodeInspectorView : BaseNodeInspectorView` with all sections (label, EndReason enum, choice add/remove/label/condition + live ports + reconnect, sub-graph target/inherit + cycle refusal via `CycleDetector`, typed parameter panel, base-node section) in `com.faolline.starterGraph/Editor/Inspector/StarterNodeInspectorView.cs`
- [x] T027 [US2] Implement `StarterGraphEditorWindow : BaseGraphEditorWindow` — toolbar Run/Choose/Continue/GoBack/GoBackToCheckpoint, drain loop pausing at `ChoiceNodeData`, `Choose`/`ChooseById`, wiring the graph view + inspector — in `com.faolline.starterGraph/Editor/Window/StarterGraphEditorWindow.cs`

**Checkpoint**: A graph with all node types is authorable and runnable from the editor (SC-001).

---

## Phase 5: User Story 3 — Robustness & ergonomics (Priority: P3)

**Goal**: Edge reconnect + no-data-loss on reload (inherited), multi-window, cycle refusal, and a generated sample graph.

**Independent Test**: Reload a graph → edges drawn, data intact. Open two graphs → two windows. Assign a cyclic sub-graph → refused. Generate the sample → it runs to completion.

### Tests for User Story 3 ⚠️ (write first, must FAIL)

- [x] T028 [P] [US3] Write failing tests: reloading a graph keeps its data and reconnects edges; switching graphs preserves both datasets; removing a choice keeps surviving choice edges connected — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterReloadTests.cs`
- [x] T029 [P] [US3] Write failing tests: assigning a recursive sub-graph target is refused (reverts + logs); a runtime sub-graph cycle throws `GraphCycleException` — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterCycleTests.cs`
- [x] T030 [P] [US3] Write failing test: the sample builder generates a graph that runs to completion (descends a sub-graph, pauses at a choice with typed-condition filtering) — in `com.faolline.starterGraph/Tests/EditMode/Editor/StarterSampleTests.cs`

### Implementation for User Story 3

- [x] T031 [US3] Add per-asset multi-window to `com.faolline.starterGraph/Editor/Window/StarterGraphEditorWindow.cs`: `OnOpenAsset` focuses an existing window showing the asset, else `CreateWindow` titled by the asset name
- [x] T032 [P] [US3] Implement `StarterSampleBuilder` (editor menu generating a parent + child `StarterGraph` exercising choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters) in `com.faolline.starterGraph/Editor/Samples/StarterSampleBuilder.cs`

**Checkpoint**: Reload/no-data-loss, multi-window, cycle refusal, and a runnable sample all verified (SC-004, SC-005, SC-006, SC-007).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T033 Run the full EditMode suite via Unity Test Runner and confirm all starterGraph + prior tests are green (no regression — SC-008)
- [ ] T034 Validate the quickstart walkthroughs manually in the editor (US1 headless, US2 author/run all node types, US3 reload/multi-window/cycle/sample)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately.
- **US1 (Phase 3)**: depends on Setup; it is the runtime foundation.
- **US2 (Phase 4)**: depends on US1 (uses the runtime types).
- **US3 (Phase 5)**: depends on US2 (editor) — adds multi-window + sample, verifies inherited robustness.
- **Polish (Phase 6)**: after the desired stories.

### Within / across stories

- Tests are written first and MUST FAIL before implementation (Constitution IV).
- Runtime types (US1) before editor (US2); editor before robustness/sample (US3).
- `StarterGraphEditorWindow.cs` is touched by T027 (US2) and T031 (US3) → keep those **sequential**.
- Node views/conditions/actions are independent files → `[P]` within their step.

### Parallel Opportunities

- All Setup asmdefs (T002–T004) are `[P]`.
- US1 test tasks T005–T010 are `[P]`; runtime impl T011/T012/T014/T015/T016/T018 are `[P]` (T013 after T012; T017 after T016).
- US2 test tasks T019–T022 are `[P]`.
- US3 test tasks T028–T030 are `[P]`.

---

## Parallel Example: User Story 1

```text
# Launch the US1 test tasks together (different files):
Task: "T005 StarterGraph test"
Task: "T006 StarterContext contract test"
Task: "T007 StarterChoice test"
Task: "T008 Conditions tests"
Task: "T009 Actions tests"
Task: "T010 GoBack typed-context restore test"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. US1 runtime socle → **STOP & VALIDATE** (headless logic on the starter) → ship.

### Incremental Delivery

1. Setup → US1 (runtime, MVP) → US2 (full editor) → US3 (robustness + sample). Each story is independently testable and adds value.

---

## Notes

- [P] = different files, no dependency.
- No graphcore changes — editor robustness (LoadGraph data-safety, edge reconnect, cycle detection) is inherited from graphcore's `BaseGraphView`/`CycleDetector`.
- Mirror the validated `com.faolline.graphTest` types when implementing each `Starter*` equivalent.
- Conditions/actions keep the generic `BaseContext` signature; keys are serialized data, not call-site literals (Principle VI).
- Commit after each task or logical group; run the suite at each checkpoint.
