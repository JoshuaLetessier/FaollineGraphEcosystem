---
description: "Task list for 016-reactive-engine (P3 — ReactiveEvaluator in com.faolline.graphstandard)"
---

# Tasks: P3 — Reactive engine (ReactiveEvaluator)

**Input**: Design documents from `specs/016-reactive-engine/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED — constitution mandates TDD. EditMode only. Run via Unity 6000.3 batchmode (editor
CLOSED; `-runTests -testPlatform EditMode` WITHOUT `-quit`; re-run once after source changes; verify the
results XML — see memory `maximize-headless-testing`).

**Organization**: by user story. Branch `016-reactive-engine` (P1+P2 included). graphcore is UNTOUCHED.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different file, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 / US4 (omitted for Setup / Polish)

## Path Conventions

New lib root: `com.faolline.graphstandard/`. Repository-relative.

---

## Phase 1: Setup — create the graphstandard package

**Purpose**: stand up the new buffer lib so the engine has a home that compiles.

- [ ] T001 Create `com.faolline.graphstandard/package.json` (name `com.faolline.graphstandard`, version `0.1.0`, unity `6000.0`, displayName/description, `dependencies: { "com.faolline.graphcore": "0.0.0" }`, author).
- [ ] T002 [P] Create `com.faolline.graphstandard/Runtime/com.faolline.graphstandard.Runtime.asmdef` (name `com.faolline.graphstandard.Runtime`, rootNamespace `Faolline.GraphStandard`, references `["com.faolline.graphcore.Runtime"]`).
- [ ] T003 [P] Create `com.faolline.graphstandard/Tests/EditMode/com.faolline.graphstandard.Tests.EditMode.asmdef` (name `com.faolline.graphstandard.Tests.EditMode`, rootNamespace `Faolline.GraphStandard.Tests`, references Runtime + `com.faolline.graphcore.Runtime` + `UnityEngine.TestRunner` + `UnityEditor.TestRunner`, `includePlatforms:["Editor"]`, `testPlatforms:["EditMode"]`, `autoReferenced:false`, `overrideReferences:true`, `precompiledReferences:["nunit.framework.dll"]`).
- [ ] T004 Batchmode compile pass to let Unity import the package and generate `.meta` GUIDs; confirm the assemblies compile empty (no errors in the log).

**Checkpoint**: graphstandard exists and compiles — engine work can begin.

---

## Phase 2: Foundational — node-state type

- [ ] T005 Create `com.faolline.graphstandard/Runtime/Reactive/ReactiveNodeState.cs` — `public enum ReactiveNodeState { Locked = 0, Available = 1, Completed = 2 }` with XML docs.

---

## Phase 3: User Story 1 — State derivation (Priority: P1) 🎯 MVP

**Goal**: derive Locked/Available/Completed per node from topology + completed-set (AND prerequisites).

**Independent Test**: DAG A,B→C — empty set: A,B Available, C Locked; {A}: B Available, C Locked; {A,B}: C Available.

### Tests (write FIRST) ⚠️

- [ ] T006 [P] [US1] Write `ReactiveStateDerivationTests` in `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveStateDerivationTests.cs`: no-prereq node Available (INV-1); C requiring A,B is Locked with {A}, Available with {A,B} (INV-1/INV-2); a node id in the completed-set is Completed regardless of prereqs (INV-1); GetState unknown id → Locked; AvailableNodeIds / CompletedNodeIds reflect derivation (INV-5). Confirm RED.

### Implementation

- [ ] T007 [US1] Create `com.faolline.graphstandard/Runtime/Reactive/ReactiveEvaluator.cs`: ctor `(BaseGraph, BaseContext, string completedSetKey)` builds the prerequisites map (incoming-edge sources) and runs initial evaluation; derivation rule per data-model; `GetState`, `AvailableNodeIds`, `CompletedNodeIds`. `[GraphStandard]` prefix; XML docs. Confirm T006 GREEN.

**Checkpoint**: state derivation works (queries only).

---

## Phase 4: User Story 2 — Cascade unlocks (Priority: P1)

**Goal**: MarkCompleted adds to the completed-set and re-evaluates; dependents unlock; idempotent re-mark.

**Independent Test**: A,B→C. Mark A → C Locked. Mark B → C Available. Mark A again → no change.

### Tests (write FIRST) ⚠️

- [ ] T008 [P] [US2] Write `ReactiveCascadeTests` in `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveCascadeTests.cs`: marking the last missing prerequisite flips a dependent to Available (INV-2/INV-3); the completed-set contains a marked id (INV-3/INV-7); re-marking an already-completed id is a no-op (no duplicate count change) (INV-3). Confirm RED.

### Implementation

- [ ] T009 [US2] Add `MarkCompleted(string nodeId)` + `Reevaluate()` to `ReactiveEvaluator` (MarkCompleted: no-op if already in set, else AddToCollection then Reevaluate; Reevaluate recomputes all states). Confirm T008 GREEN.

**Checkpoint**: US1 + US2 = the MVP (derive + cascade).

---

## Phase 5: User Story 3 — Events on state change (Priority: P2)

**Goal**: OnNodeAvailable/OnNodeCompleted fire on transitions; init emits for initially non-Locked nodes; no spurious.

**Independent Test**: init A,B→C → Available(A),Available(B); mark A,B → Completed(A),Completed(B),Available(C) once; re-mark → nothing.

### Tests (write FIRST) ⚠️

- [ ] T010 [P] [US3] Write `ReactiveEventTests` in `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveEventTests.cs`: init over A,B→C emits OnNodeAvailable for A and B, not C (INV-4); marking B (A already done) emits OnNodeCompleted(B) then OnNodeAvailable(C) exactly once (INV-4); re-mark emits nothing (INV-3/INV-4); a node already in the set at construction emits OnNodeCompleted at init (INV-4). Confirm RED.

### Implementation

- [ ] T011 [US3] Add `event Action<string> OnNodeAvailable` / `OnNodeCompleted` to `ReactiveEvaluator`; emit on transitions in Initialize and Reevaluate using the `_states` cache (entry-into-Available/Completed only; no event for unchanged or →Locked). Confirm T010 GREEN.

**Checkpoint**: all three functional stories done.

---

## Phase 6: User Story 4 — Durable & reversible (Priority: P2 / SC-006)

**Goal**: re-evaluation is reversible (re-pass); a game-like multi-tier progression DAG works headless.

**Independent Test**: complete A,B,C; restore ctx to set {A}; Reevaluate → C Locked, B Available, no side-effects.

### Tests (write FIRST) ⚠️

- [ ] T012 [P] [US4] Write `ReactiveProgressionDagTests` in `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveProgressionDagTests.cs`: build a game-like DAG (e.g. Crank,RepairLadder→Gate; Gate,Simon→RegionDone). Drive completions, then restore the context to a smaller completed-set (DeepClone earlier, CopyValuesFrom back via a runner GoBack OR by clearing/re-adding) and `Reevaluate`; assert the smaller satisfied set (INV-6); assert states depend only on the current set (idempotent, INV-1/INV-6). Confirm RED.

### Implementation

- [ ] T013 [US4] Ensure `Reevaluate()` is public and derives purely from the current completed-set (already from T009/T011); add any missing reversibility handling (e.g. a node Completed→Available/Locked transition updates the cache). Confirm T012 GREEN.

---

## Phase 7: Polish & Finalize

- [ ] T014 [P] Verify XML docs on all public API (`ReactiveNodeState`, `ReactiveEvaluator` ctor/events/methods/queries) and `[GraphStandard]` prefix on any log.
- [ ] T015 Full batchmode EditMode run (editor closed, no `-quit`), verify the results XML: graphcore+graphTest 586 unchanged green + graphstandard tests green. Re-confirm SC-002/SC-005 (graphcore untouched).
- [ ] T016 [P] Validate `quickstart.md` snippets compile and behave as documented; fix drift if any.

---

## Dependencies & Execution Order

- **Setup (T001-T004)** → package must compile before engine code.
- **Foundational (T005)** → the enum, used by US1+.
- **US1 (T006→T007)** → derivation; MVP core.
- **US2 (T008→T009)** → after US1 (MarkCompleted/Reevaluate extend the evaluator).
- **US3 (T010→T011)** → after US2 (events fire from Reevaluate).
- **US4 (T012→T013)** → after US3 (reversibility + scenario).
- **Polish (T014-T016)** → last; T015 after everything.

## Parallel Opportunities

- T002 [P] and T003 [P] (different asmdef files) together.
- Test-authoring T006 / T008 / T010 / T012 are different files ([P]) but each precedes its implementation
  (and US2/US3/US4 implementations extend the same ReactiveEvaluator.cs, so T009/T011/T013 are sequential).
- T014 [P] and T016 [P] independent polish.

## Implementation Strategy

### MVP (US1 + US2)

1. T001-T004 stand up graphstandard.
2. T005 enum.
3. US1: T006 (RED) → T007 (GREEN) — derivation.
4. US2: T008 (RED) → T009 (GREEN) — cascade. **STOP & VALIDATE** — a progression DAG unlocks correctly.

### Incremental

5. US3 (T010→T011) — events.
6. US4 (T012→T013) — durable/reversible + game-like scenario.
7. Finalize (T014-T016) — docs, full green, graphcore untouched.

## Notes

- graphcore Runtime is **NOT** edited by this feature (SC-005). Only the new `com.faolline.graphstandard/`
  tree and the spec docs change.
- The non-breakage gate: the existing 586 graphcore+graphTest tests pass UNCHANGED.
- No `MonoBehaviour`/`UnityEvent`; one class per file; `[GraphStandard]` prefix; XML docs on new public API.
