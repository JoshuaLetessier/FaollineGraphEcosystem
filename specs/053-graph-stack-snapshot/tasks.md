# Tasks: Full Graph-Stack Save & Restore

**Input**: Design documents from `/specs/053-graph-stack-snapshot/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (constitution IV, TDD). Every implementation task is preceded by a failing-test task, confirmed red in a headless run.

**Test runner** (dev project Editor closed): `"C:/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe" -batchmode -projectPath H:/repos/Perso/LIB_Unity/FaollineGraphEcosystem -runTests -testPlatform EditMode [-testFilter <regex>] -testResults <xml> -logFile <log>`, **without** `-quit`. A red run must fail on assertions or `NotImplementedException`, not on a compile error. New members are therefore stubbed first.

**Paths**: relative to the repo root (`Assets/FaollineGraphEcosystem`).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Record the pre-change baseline: full EditMode suite headless on the branch HEAD, results to `../../Logs/053-baseline.xml` (counts + any pre-existing failures noted in this file's Notes)

---

## Phase 2: Foundational — graphcore path model and pure check (blocks every story)

**Goal**: the immutable path description and `GraphRunPath.TryResolve` (data-model "Validation"), used by every restore entry point.

- [X] T002 [P] Add `GraphRunContextMode` (Shared = 0, Scoped = 1, Fresh = 2) in `com.faolline.graphcore/Runtime/Execution/GraphRunContextMode.cs`
- [X] T003 [P] Add immutable `GraphRunFrame` in `com.faolline.graphcore/Runtime/Execution/GraphRunFrame.cs`
- [X] T004 [P] Add `GraphRunPathErrorReason`, `GraphRunPathError`, `GraphRunPathException` in `com.faolline.graphcore/Runtime/Execution/` (one file each)
- [X] T005 Add `GraphRunPath` with both `TryResolve` overloads stubbed (`NotImplementedException`) in `com.faolline.graphcore/Runtime/Execution/GraphRunPath.cs`
- [X] T006 Write `ExecutionPathValidationTests` in `com.faolline.graphcore/Tests/EditMode/Execution/ExecutionPathValidationTests.cs`: valid 1/2/3-level paths resolve the right graphs; each reason (`NullRootGraph`, `EmptyPath`, null frame, `RootGraphMismatch`, `RepeatedGraph` on cyclic content, `NodeMissing` incl. empty id, `NotASubGraphNode`, `SubGraphTargetMissing`, `SubGraphTargetMismatch`, `ContextModeMismatch`) with its `FrameIndex`/`DeepestValidFrameIndex`; `Message` names the frame; no side effect (graphs unchanged, no log)
- [X] T007 Run T006 headless → confirm red (NotImplementedException)
- [X] T008 Implement `GraphRunPath.TryResolve` per research R4 in `com.faolline.graphcore/Runtime/Execution/GraphRunPath.cs`
- [X] T009 Run T006 → green

**Checkpoint**: the path check is complete and pure.

---

## Phase 3: User Story 1 — Resume a save taken inside a sub-graph (P1) 🎯 MVP

**Goal**: read the path off a live run, rebuild it from the root, re-enter only the deepest node, continue like a played run. Shared-context frames only in this phase (scoped/fresh in US5).

**Independent test**: C → Z (and C → Z → R) captured inside the child, JSON round-trip, restored from C: active node correct, finishing the child continues in the parent up to the root's end.

### graphcore

- [X] T010 [US1] Stub `BaseRunner.GetExecutionPath()` and `BaseRunner.StartFromPath(...)` (`NotImplementedException`) in `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs`
- [X] T011 [US1] Write `ExecutionPathTests` (read part) in `com.faolline.graphcore/Tests/EditMode/Execution/ExecutionPathTests.cs`: empty before `Start`; 1/2/3 levels root-first with ids and nodes; modes Shared/Scoped/Fresh read from the live run; top frame on a `SubGraphNodeData` before `Proceed`; never throws
- [X] T012 [US1] Add restore tests to `ExecutionPathTests.cs` (shared frames): 2- and 3-level restore → `CurrentNode`/`CurrentGraph` correct; `OnNodeEntered` fires once, for the top node only; parent `SubGraphNodeData` enter action and executor NOT re-run; top node's enter action re-run once; continuation through each `End` back into the parent, `OnEnded` once with the root's reason; top frame on a `SubGraphNodeData` → `Proceed` enters the child entry; history empty (`GoBack` no-op) and capped by the root's `HistoryDepth`; shared write in the child visible in the root context
- [X] T013 [US1] Add to `ExecutionPathTests.cs`: invalid path → `GraphRunPathException` carrying the error; a run already in progress keeps its `CurrentNode`/`State`; null root/path/context → `ArgumentNullException`
- [X] T014 [US1] Run T011–T013 → confirm red
- [X] T015 [US1] Implement `GetExecutionPath()` (research R3) in `BaseRunner.cs`
- [X] T016 [US1] Implement `StartFromPath(...)` (research R5) for Shared frames; Scoped/Fresh → `NotSupportedException` until US5, in `BaseRunner.cs`
- [X] T017 [US1] Run graphcore tests → green (US1 + Phase 2)

### graphsave

- [X] T018 [US1] Stub `GraphRunSnapshot.Frame`, `Frames`, `HasStack`, `RootGraphId`, `GetExecutionPath()`, `TryValidate(...)` (`NotImplementedException` where a body is needed) in `com.faolline.graphsave/Runtime/GraphRunSnapshot.cs`
- [X] T019 [US1] Write `GraphStackSnapshotTests` (US1 part) in `com.faolline.graphsave/Tests/EditMode/GraphStackSnapshotTests.cs`: `Capture(runner, ctx)` at 1/2/3 levels fills `Frames` root-first, `GraphId`/`CurrentNodeId` = deepest, `RootGraphId` = root, `HasStack`; JsonUtility round-trip keeps frames and modes; `Restore(runner, root, ctx)` at 2/3 levels → deepest node, values restored, continuation into the root to its end; consumer pattern: capture inside `OnNodeEntered` of the child's Start leaves the run intact and the snapshot restorable
- [X] T020 [US1] Run T019 → confirm red
- [X] T021 [US1] Implement frames mapping, `Capture(runner, ctx)` path recording, `GetExecutionPath()`, `HasStack`, `RootGraphId`, and the stacked branch of `Restore` (`ApplyTo` + `StartFromPath`) in `GraphRunSnapshot.cs`
- [X] T022 [US1] Run graphsave tests → green (existing `GraphSaveTests` unchanged)

**Checkpoint**: a save inside a sub-graph resumes inside it and returns to the root (spec US1).

---

## Phase 4: User Story 2 — Mismatches are reported, never guessed (P1)

**Goal**: pure `TryValidate`, and loud, all-or-nothing refusal in `Restore`.

**Independent test**: each mismatch kind is reported with reason and frame; a refused restore leaves context and runner untouched.

- [X] T023 [US2] Add to `GraphStackSnapshotTests.cs`: `TryValidate` success (no side effect) and failures (wrong root, node removed, sub-graph retargeted) passing graphcore's error through; `Restore` on a mismatch logs `[GraphSave] Restore refused: …`, leaves the context values untouched (no `ApplyTo`) and the runner idle; a single-level stacked snapshot restored into another graph fails (FR-015)
- [X] T024 [US2] Run T023 → confirm red
- [X] T025 [US2] Implement `TryValidate` and the check-before-apply guard in `Restore` (research R6) in `GraphRunSnapshot.cs`
- [X] T026 [US2] Run graphsave tests → green

**Checkpoint**: no restore path can apply part of a mismatched save.

---

## Phase 5: User Story 3 — Driver boot restores the whole stack (P2)

**Goal**: `GraphFlowDriver.Boot(snapshot)` with `Graph` = root.

**Independent test**: driver with root C boots from a save in Z, finishes Z, continues into C with driver events.

- [X] T027 [US3] Write `GraphFlowDriverSnapshotTests` in `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverSnapshotTests.cs`: stacked boot from root → `IsRunning`, `Runner.CurrentGraph` = child, `CurrentNode` = saved, advancing through the child's End continues in the root, `OnEnded` once; wrong root → `[GraphGameFlow]` error, inert, supplied context untouched; re-entrant `Boot(snapshot)` from `OnEnded` has the same outcome; consumer pattern end-to-end (capture on Start/checkpoint `OnNodeEntered` with `AutoAdvance`, JSON, a new driver boots from the root)
- [X] T028 [US3] Run T027 → confirm red
- [X] T029 [US3] Implement the stacked branch of `Boot(snapshot)` (research R10) in `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs`
- [X] T030 [US3] Run graphgameflow tests → green

---

## Phase 6: User Story 4 — Existing saves and code keep working (P2)

**Goal**: no-stack behavior and both serializers.

- [X] T031 [P] [US4] Add to `GraphStackSnapshotTests.cs`: old JSON without `Frames` → `HasStack == false`, `RootGraphId == GraphId`, `GetExecutionPath()` empty, `TryValidate` true, `Restore` resumes in the supplied graph exactly as before; explicit-ids `Capture` → no stack
- [X] T032 [P] [US4] Add to `com.faolline.graphsave.savesystem/Tests/EditMode/SaveSystemGraphStoreTests.cs`: a stacked snapshot through the real `JsonSaveSystem` (Newtonsoft) keeps `Frames` (ids, nodes, modes), `HasStack`, `RootGraphId`
- [X] T033 [US4] Add a no-stack `Boot(snapshot)` regression test to `GraphFlowDriverSnapshotTests.cs`
- [X] T034 [US4] Run T031–T033; fix any regression found (expected green: these lock existing behavior)

---

## Phase 7: User Story 5 — Scoped and fresh sub-graphs (P3)

**Goal**: rebuild Scoped (D2-A) and Fresh (D1-A) frames; report lossy fresh captures.

- [X] T035 [US5] Add to `ExecutionPathTests.cs`: scoped restore → `HasLocalContext`, declared local default readable, undeclared write stays local and vanishes on the child's End, globals intact; fresh restore → child context separate from the root, seeded with declared defaults, root untouched by child writes, root context back after End; two nested scoped frames reproduce the live run's layering (same warning)
- [X] T036 [US5] Add to `GraphStackSnapshotTests.cs`: capture with a fresh enclosing frame or a fresh deepest frame off its entry → one `[GraphSave] Capture: frame …` error, snapshot still returned; fresh deepest frame on its entry → no error; scoped frames → no message; restore of a fresh frame → defaults
- [X] T037 [US5] Run T035–T036 → confirm red
- [X] T038 [US5] Implement Scoped and Fresh frame rebuilding in `StartFromPath` (research R5, R11) in `BaseRunner.cs`
- [X] T039 [US5] Implement the fresh-frame capture report (research R7) in `GraphRunSnapshot.cs`
- [X] T040 [US5] Run graphcore + graphsave tests → green

---

## Phase 8: Polish, documentation, release metadata

- [X] T041 [P] graphcore docs: `README.md` (execution path section next to "SubGraph nesting"/`StartFrom`), `CHANGELOG.md` [0.44.0], `package.json` 0.44.0 in `com.faolline.graphcore/`
- [X] T042 [P] graphsave docs: replace "Restore is top-level only" in `README.md` with the stack behavior + limits; `CHANGELOG.md` [0.11.0] (incl. the single-level behavior note); `package.json` 0.11.0 + graphcore floor 0.44.0; XML docs of `Capture`/`Restore` in `GraphRunSnapshot.cs` in `com.faolline.graphsave/`
- [X] T043 [P] graphgameflow docs: `README.md` (`Boot(GraphRunSnapshot)` + "Multiple root graphs" use `RootGraphId`), `IGraphCatalog.cs` XML doc, `Boot(snapshot)` XML doc, `CHANGELOG.md` [0.20.0], `package.json` 0.20.0 + floors graphcore 0.44.0 / graphsave 0.11.0 in `com.faolline.graphgameflow/`
- [X] T044 [P] graphdialoguesystem docs (D4-A): XML docs on `DialoguePlayer.SaveState`/`RestoreFrom` and `DialogueSessionState`, `CHANGELOG.md` [0.20.1], `package.json` 0.20.1 in `com.faolline.graphdialoguesystem/`
- [X] T045 Ecosystem version/floor sync: script-driven check of every `package.json` version vs every floor and every README header/root `README.md` table/`com.faolline.graphcore/Editor/GraphEcosystemModules.json` (recurring drift habit); fix what lags, never declare a floor above its target
- [X] T046 Full EditMode suite headless; compare with the T001 baseline (new tests all green, no new failure)
- [X] T047 Review against spec FR-001…FR-020 / SC-001…SC-006; FR-013's editor-probe wiring is checked by code review (the probe only wires in Play mode, so EditMode tests cannot observe it)
- [X] T048 Atomic commits on the branch (specs; graphcore; graphsave + savesystem test; graphgameflow; graphdialoguesystem; ecosystem sync), no co-author trailer, no tag, no push, no merge
- [X] T049 Consumer summary (capture, restore from a supplied root, what `RootGraphId` gives, how a mismatch is reported) in the final report

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 (graphcore then graphsave) → US2 → US3 → US4 → US5 → Polish.
- US2 needs US1's stacked `Restore` branch. US3 needs US1 + US2 (`TryValidate`). US4 only needs US1/US2 and could run any time after them. US5 extends `StartFromPath` (US1) and `Capture` (US1).
- Within a phase: stub → tests → red run → implement → green run.

## Parallel Opportunities

- T002/T003/T004 (separate files).
- T031/T032 (separate packages).
- T041–T044 (separate packages' docs).

## Implementation Strategy

MVP = Phases 1–4: the consumer can already restore through `GraphRunSnapshot.Restore`. Phase 5 makes it reach the driver they actually use. Phases 6–7 lock compatibility and the non-shared modes before release.

## Notes

- Baseline (T001): 1471/1471 EditMode tests passed on the branch HEAD before any change (`Logs/053-baseline.xml`).
- T007/T014 were run as one red batch (34/34 failing on `NotImplementedException`), then T009/T017 as one green batch (34/34).
- Full suite after implementation (T046): 1535/1535 passed (baseline 1471 + 64 new tests: graphcore 38, graphsave 19, graphsave.savesystem 1, graphgameflow 6), no failure (`Logs/053-full.xml`).
- Each phase was red before its implementation: graphcore 34/34 `NotImplementedException`; graphsave US1 7/7 and US2 6 failing; driver 5 failing (the no-stack regression test passed as expected); US5 7 failing (`NotSupportedException` / missing capture error).
- FR-013's editor-probe wiring is verified by code review only: `EditorWireProbe` returns early outside Play mode, so EditMode tests cannot observe it.
- D5 (names) was not answered by the user: the candidate names shipped and can still be renamed before merge.

