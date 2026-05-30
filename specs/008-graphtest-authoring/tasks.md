---
description: "Task list for GraphTest — Editor Authoring Gaps"
---

# Tasks: GraphTest — Editor Authoring Gaps

**Input**: Design documents from `specs/008-graphtest-authoring/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: INCLUDED — Constitution Principle IV mandates TDD (Red-Green-Refactor). Test tasks are authored first and must FAIL before implementation.

**Organization**: Grouped by user story. Stories are independent and can ship in priority order (US1 = MVP).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no incomplete dependency)
- **[Story]**: US1 / US2 / US3
- Paths are relative to the package root `com.faolline.graphTest/`

## Path Conventions

Extension of the existing `com.faolline.graphTest` package — no new asmdefs, no graphcore edits. Runtime under `Runtime/`, editor under `Editor/`, EditMode tests under `Tests/EditMode/`.

---

## Phase 1: Setup

**Purpose**: Confirm target folders exist (all already present from prior features).

- [x] T001 Verify the folders `com.faolline.graphTest/Runtime/Conditions/`, `com.faolline.graphTest/Runtime/Actions/`, and `com.faolline.graphTest/Editor/Nodes/` exist (create any missing) for the new types

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None required — the three user stories are independent and rely only on existing graphcore runtime (EndReason, SubGraphNodeData, BaseContext typed parameters, CycleDetector). No shared new infrastructure blocks the stories.

**Checkpoint**: Proceed directly to user stories.

---

## Phase 3: User Story 1 — Set an End node's End Reason (Priority: P1) 🎯 MVP

**Goal**: When an End node is selected, the inspector shows an editable EndReason selector (Completed/Cancelled/Error); the choice persists across save/reload and is reported at Run.

**Independent Test**: Add an End node, set reason = Cancelled, save/reload (still Cancelled), Run a graph reaching it → console logs `Graph ended: Cancelled`.

### Tests for User Story 1 ⚠️ (write first, must FAIL before implementation)

- [x] T002 [P] [US1] Write failing test: `BindNode` with an `EndNodeData` does not throw and the inspector exposes an EndReason control; a public `SetEndReason(EndNodeData, EndReason)` helper updates the node's `EndReason` and marks the graph dirty — in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorViewTests.cs`
- [x] T003 [P] [US1] Write failing save/reload fidelity test: an `EndNodeData` with `EndReason = Cancelled` round-trips (via `Object.Instantiate`) with the reason preserved — in `com.faolline.graphTest/Tests/EditMode/Runtime/TestGraphTests.cs`

### Implementation for User Story 1

- [x] T004 [US1] In `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs`, dispatch in `BindNode` on `node is EndNodeData` to render an `EnumField` initialized to `EndReason`; on change set `endNode.EndReason` + mark dirty. Add a public `SetEndReason(EndNodeData, EndReason)` helper (testability, mirrors `AddChoice` pattern)

**Checkpoint**: End nodes have an editable, persistent EndReason reported at Run (SC-001, SC-002). MVP deliverable.

---

## Phase 4: User Story 2 — Author and run a Sub-Graph node (Priority: P2)

**Goal**: Add a SubGraph node (menu + view with one in/one out), assign its TargetGraph + InheritParentContext in the inspector (recursive target refused), and have Run descend into the child graph and resume the parent.

**Independent Test**: child graph Start→Statement→End; parent Start→SubGraph(child)→End; Run shows the child's nodes between entering and leaving the SubGraph node, then the parent completes.

### Tests for User Story 2 ⚠️ (write first, must FAIL before implementation)

- [x] T005 [P] [US2] Write failing test for `SubGraphNodeView`: one `"in"` input port and one `"out"` output port — in `com.faolline.graphTest/Tests/EditMode/Editor/SubGraphNodeViewTests.cs`
- [x] T006 [P] [US2] Write failing test: "Add SubGraph Node" creates a `SubGraphNodeData` and `CreateNodeView` dispatches to a `SubGraphNodeView` — in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphViewAddNodeTests.cs`
- [x] T007 [P] [US2] Write failing test for the SubGraph inspector section: public helpers set `TargetGraph` and `InheritParentContext`; assigning a `TargetGraph` that references the current graph is refused (value reverts, cycle logged) — in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorViewTests.cs`
- [x] T008 [P] [US2] Write failing execution test: a parent graph with a SubGraph node pointing at a child runs the child to completion and resumes the parent (visited-node order) — in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphExecutionTests.cs`
- [x] T009 [P] [US2] Write failing save/reload fidelity test: a `SubGraphNodeData` round-trips with `TargetGraph` and `InheritParentContext` preserved — in `com.faolline.graphTest/Tests/EditMode/Runtime/TestGraphTests.cs`

### Implementation for User Story 2

- [x] T010 [US2] Implement `SubGraphNodeView : BaseNodeView` (title "SubGraph", `"in"` `Capacity.Multi` + `"out"` `Capacity.Single`, typed `TestEdgeView`) in `com.faolline.graphTest/Editor/Nodes/SubGraphNodeView.cs`
- [x] T011 [US2] In `com.faolline.graphTest/Editor/Graph/TestGraphView.cs`, add "Add SubGraph Node" to `BuildContextualMenu` (creates `SubGraphNodeData`) and a `CreateNodeView` case `SubGraphNodeData.NodeTypeId → new SubGraphNodeView(...)`
- [x] T012 [US2] In `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs`, add the SubGraph section: when `node is SubGraphNodeData`, render an `ObjectField` (`BaseGraph`) for `TargetGraph` with a cycle guard on change (`CycleDetector.Check(_graph, proposed)` → revert + `[GraphTest] Cycle refused` log) and a `Toggle` for `InheritParentContext`; add public helpers `SetSubGraphTarget`/`SetInheritParentContext` (mutate + SetDirty)

**Checkpoint**: SubGraph nodes are authorable, cycle-safe, persistent, and run end-to-end (SC-003, SC-004, SC-005).

---

## Phase 5: User Story 3 — Typed Int/Float/String parameters (Priority: P3)

**Goal**: Declare Int/Float/String parameters (type + default) in the panel, set/test them via typed actions/conditions, and route conditionally on non-boolean values.

**Independent Test**: Int `score`; Start→Statement(set score=5)→Choice gated by `score ≥ 3` → branches; choice available at 5, filtered at 1.

### Tests for User Story 3 ⚠️ (write first, must FAIL before implementation)

- [x] T013 [P] [US3] Write failing tests for `TestIntCondition`, `TestFloatCondition`, `TestStringCondition` (each operator/equality case, plus null-safe false+warning on missing/mistyped key) — in `com.faolline.graphTest/Tests/EditMode/Runtime/TypedConditionTests.cs`
- [x] T014 [P] [US3] Write failing tests for `TestSetIntAction`, `TestSetFloatAction`, `TestSetStringAction` (each writes the typed value into the context) — in `com.faolline.graphTest/Tests/EditMode/Runtime/TypedActionTests.cs`
- [x] T015 [P] [US3] Write failing test: the parameter panel adds an Int/Float/String parameter (type + default) and it persists across reload; existing bool-parameter behavior still works — in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorParameterPanelTests.cs`
- [x] T016 [P] [US3] Write failing test: a choice gated by a passing `TestIntCondition` is offered and one gated by a failing `TestIntCondition` is filtered out (non-bool routing parity) — in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphEditorWindowSessionTests.cs`

### Implementation for User Story 3

- [x] T017 [P] [US3] Add the `ComparisonOperator` enum (Equal/NotEqual/Less/LessOrEqual/Greater/GreaterOrEqual) in `com.faolline.graphTest/Runtime/Conditions/ComparisonOperator.cs`
- [x] T018 [P] [US3] Implement `TestIntCondition` and `TestFloatCondition` (`ParameterKey`, `Operator`, `ExpectedValue`; `TryGet<T>` + compare; false+warn on missing/mistyped) and `TestStringCondition` (`ParameterKey`, `ExpectedValue`, `Negate`) under `com.faolline.graphTest/Runtime/Conditions/` (`[CreateAssetMenu]` each)
- [x] T019 [P] [US3] Implement `TestSetIntAction`, `TestSetFloatAction`, `TestSetStringAction` (`ParameterKey`, `Value`; `context.Set<T>`) under `com.faolline.graphTest/Runtime/Actions/` (`[CreateAssetMenu]` each)
- [x] T020 [US3] Generalize the parameter panel in `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs`: list all parameter types; add-row with key `TextField` + `ParameterType` `EnumField` + default `TextField`; `AddParameter(key, type, default)` and type-agnostic remove; keep `AddBoolParameter`/`RemoveBoolParameter` as wrappers (backward compatibility)

**Checkpoint**: Int/Float/String parameters drive conditional routing and persist (SC-006, SC-007).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T021 Run the full EditMode suite via Unity Test Runner and confirm all 008 + prior tests are green (no regression — SC-008)
- [ ] T022 Validate the quickstart walkthroughs manually in the editor (US1 EndReason; US2 SubGraph descend/resume + cycle refusal; US3 typed routing); optionally extend the sample graph generator with a SubGraph + an Int parameter

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — immediate.
- **Foundational (Phase 2)**: empty — stories are independent.
- **User Stories (Phase 3–5)**: each depends only on Setup; recommended order US1 → US2 → US3 (priority).
- **Polish (Phase 6)**: after the desired stories.

### User Story Dependencies

- **US1 (P1)**: independent. Touches `TestNodeInspectorView.cs` only.
- **US2 (P2)**: independent. Touches `SubGraphNodeView.cs` (new), `TestGraphView.cs`, `TestNodeInspectorView.cs`.
- **US3 (P3)**: independent. Touches new condition/action files + `TestNodeInspectorView.cs`.

### Shared-file note

`Editor/Inspector/TestNodeInspectorView.cs` is touched by all three stories (EndReason section, SubGraph section, parameter panel). Those **implementation** tasks (T004, T012, T020) edit the same file — keep them **sequential** across stories even though their tests are `[P]`.

### Parallel Opportunities

- All test tasks within a story are `[P]` (distinct files): T002/T003; T005–T009; T013–T016.
- US3 runtime types are `[P]`: T017, T018, T019 (different files).
- T010 (SubGraphNodeView) and T011 (TestGraphView) are different files → `[P]` within US2; T012 shares the inspector file with US1/US3 impl → sequential.

---

## Parallel Example: User Story 2

```text
# Launch the US2 test tasks together (different files):
Task: "T005 SubGraphNodeView ports test in Tests/EditMode/Editor/SubGraphNodeViewTests.cs"
Task: "T006 Add SubGraph Node + dispatch test in Tests/EditMode/Editor/TestGraphViewAddNodeTests.cs"
Task: "T007 SubGraph inspector + cycle-refusal test in Tests/EditMode/Editor/TestNodeInspectorViewTests.cs"
Task: "T008 SubGraph execution test in Tests/EditMode/Editor/TestGraphExecutionTests.cs"
Task: "T009 SubGraph save/reload test in Tests/EditMode/Runtime/TestGraphTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. US1 (EndReason) → **STOP & VALIDATE** (quickstart US1) → ship.

### Incremental Delivery

1. Setup → US1 (MVP, ship) → US2 (SubGraph, ship) → US3 (typed params, ship). Each story is independently testable and adds value without breaking the previous.

---

## Notes

- [P] = different files, no dependency.
- No graphcore changes — reuse `EndReason`, `SubGraphNodeData`, `ParameterType`, `BaseContext`, `CycleDetector`.
- Conditions/actions keep the `BaseContext` generic signature; the key is serialized data, not a call-site literal (Principle VI).
- Commit after each task or logical group; run the suite at each checkpoint.
