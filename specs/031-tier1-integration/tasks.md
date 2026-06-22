# Tasks: Tier-1 Integration Improvements

**Input**: Design documents from `/specs/031-tier1-integration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md

**Tests**: TDD required (Constitution IV). Tests written first, confirmed failing, then implementation.

**Organization**: Tasks grouped by user story. Each story is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1=PlayDialogueAction, US2=ContextWatch, US3=AutoEvaluate)

---

## Phase 1: Setup

**Purpose**: Branch creation and spec artifacts

- [ ] T001 Create feature branch `031-tier1-integration` from master

---

## Phase 2: Foundational — BaseContext Wildcard Subscriptions (graphcore)

**Purpose**: `OnAnyParameterChanged`/`OnAnyCollectionChanged` on BaseContext — required by BOTH US2 (Context Watch) and US3 (auto-evaluate). BLOCKS all user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests

- [ ] T002 [P] Write test: OnAnyParameterChanged fires on Set\<int\> in com.faolline.graphTest/Tests/EditMode/Runtime/WildcardContextSubscriptionTests.cs
- [ ] T003 [P] Write test: OnAnyParameterChanged fires for every typed Set (bool, float, string) in same file
- [ ] T004 [P] Write test: OffAnyParameterChanged stops notifications in same file
- [ ] T005 [P] Write test: OnAnyCollectionChanged fires on AddToCollection/RemoveFromCollection/ClearCollection in same file
- [ ] T006 [P] Write test: OffAnyCollectionChanged stops notifications in same file
- [ ] T007 Write test: per-key handlers fire BEFORE wildcard handlers in same file
- [ ] T008 Run tests — confirm all FAIL (methods don't exist yet)

### Implementation

- [ ] T009 Add `OnAnyParameterChanged(Action<string>)`, `OffAnyParameterChanged(Action<string>)` to com.faolline.graphcore/Runtime/Context/BaseContext.cs — fire after per-key handlers in Set\<T\>
- [ ] T010 Add `OnAnyCollectionChanged(Action<string>)`, `OffAnyCollectionChanged(Action<string>)` to same file — fire after per-key handlers in AddToCollection/RemoveFromCollection/ClearCollection
- [ ] T011 Run tests — confirm all PASS
- [ ] T012 Run full EditMode suite — confirm zero regressions

**Checkpoint**: BaseContext wildcard subscriptions green. US2 and US3 can now proceed in parallel.

---

## Phase 3: User Story 1 — PlayDialogueAction + DialogueBus (Priority: P1) 🎯 MVP

**Goal**: A gameflow node can play a dialogue with zero custom MonoBehaviour code. The developer attaches PlayDialogueAction to OnEnter + sets AwaitSignalName on the node. Any UI subscribes to DialogueBus once.

**Independent Test**: Create a DialogueGraph (Start→Line→End), a BaseContext, call PlayDialogueAction.Execute → verify DialogueBus.OnLine fires → call DialogueBus.Advance() → verify DialogueBus.OnEnded fires and the signal is raised on the context.

### Tests

- [ ] T013 [P] [US1] Write test: DialogueBus.Play starts player and fires OnDialogueStarted in com.faolline.graphdialoguesystem/Tests/EditMode/Runtime/DialogueBusTests.cs
- [ ] T014 [P] [US1] Write test: DialogueBus relays OnLine from active player in same file
- [ ] T015 [P] [US1] Write test: DialogueBus.Advance routes to active player and drains to next step in same file
- [ ] T016 [P] [US1] Write test: DialogueBus relays OnChoices and Choose routes correctly in same file
- [ ] T017 [P] [US1] Write test: DialogueBus.OnEnded fires and clears ActivePlayer when dialogue ends in same file
- [ ] T018 [P] [US1] Write test: DialogueBus.Play while already playing stops previous and starts new in same file
- [ ] T019 [P] [US1] Write test: PlayDialogueAction.Execute starts bus and raises signal on end in com.faolline.graphdialoguesystem/Tests/EditMode/Runtime/PlayDialogueActionTests.cs
- [ ] T020 [P] [US1] Write test: PlayDialogueAction with null graph logs warning and does not park in same file
- [ ] T021 [P] [US1] Write test: PlayDialogueAction auto-derives signal name from GraphId when SignalName is empty in same file
- [ ] T022 [US1] Run tests — confirm all FAIL

### Implementation

- [ ] T023 [US1] Implement DialogueBus static class in com.faolline.graphdialoguesystem/Runtime/Playback/DialogueBus.cs — Play(), Advance(), Choose(), RaiseSignal(), Tick(), Stop(), all events
- [ ] T024 [US1] Implement PlayDialogueAction in com.faolline.graphdialoguesystem/Runtime/Actions/PlayDialogueAction.cs — Execute calls DialogueBus.Play with onEnded callback that raises signal
- [ ] T025 [US1] Run tests — confirm all PASS
- [ ] T026 [US1] Run full EditMode suite — confirm zero regressions

**Checkpoint**: PlayDialogueAction + DialogueBus functional. A gameflow node with this action + AwaitSignalName plays a dialogue end-to-end.

---

## Phase 4: User Story 2 — Context Watch Editor Window (Priority: P2)

**Goal**: An EditorWindow shows the live BaseContext parameters and collections during Play Mode, event-driven via the new wildcard subscriptions.

**Independent Test**: In Play Mode, open Context Watch, run a graph with a GraphFlowDriver, verify parameters and collections appear and update on change. (Visual — EditMode tests cover the registry.)

### Tests

- [ ] T027 [P] [US2] Write test: GraphRunContextRegistry.Register stores context and GetContext retrieves it in com.faolline.graphTest/Tests/EditMode/Editor/GraphRunContextRegistryTests.cs
- [ ] T028 [P] [US2] Write test: GraphRunContextRegistry.Unregister removes entry and GetContext returns null in same file
- [ ] T029 [P] [US2] Write test: GraphRunContextRegistry.GetContext returns null for unknown probe in same file
- [ ] T030 [US2] Run tests — confirm all FAIL

### Implementation

- [ ] T031 [US2] Implement GraphRunContextRegistry static class in com.faolline.graphcore/Editor/Registry/GraphRunContextRegistry.cs — Register, Unregister, GetContext (editor-only)
- [ ] T032 [US2] Modify BaseRunner.EditorWireProbe to also register context with GraphRunContextRegistry in com.faolline.graphcore/Runtime/Execution/BaseRunner.cs (editor-only section)
- [ ] T033 [US2] Modify BaseRunner.EditorUnwireProbe (OnDestroy/Stop path) to unregister from GraphRunContextRegistry in same file
- [ ] T034 [US2] Implement ContextWatchWindow EditorWindow in com.faolline.graphcore/Editor/Window/ContextWatchWindow.cs — probe dropdown, parameter table, collection table, event-driven repaint via GraphRunMonitor.Changed
- [ ] T035 [US2] Run tests — confirm all PASS
- [ ] T036 [US2] Run full EditMode suite — confirm zero regressions

**Checkpoint**: Context Watch window shows live context in Play Mode.

---

## Phase 5: User Story 3 — QuestEvaluator Auto-Evaluate (Priority: P3)

**Goal**: QuestEvaluator can opt into push-mode evaluation via EnableAutoEvaluate(), eliminating Update() polling.

**Independent Test**: Create a QuestGraph with a BoolCondition objective, enable auto-evaluate, change the bool in context → verify OnObjectiveStateChanged fires without explicit Evaluate() call.

### Tests

- [ ] T037 [P] [US3] Write test: EnableAutoEvaluate causes Evaluate on context parameter change in com.faolline.graphquest/Tests/EditMode/QuestEvaluatorAutoEvaluateTests.cs
- [ ] T038 [P] [US3] Write test: EnableAutoEvaluate causes Evaluate on context collection change in same file
- [ ] T039 [P] [US3] Write test: DisableAutoEvaluate stops auto-evaluation in same file
- [ ] T040 [P] [US3] Write test: EnableAutoEvaluate twice is idempotent (no double-subscribe) in same file
- [ ] T041 [P] [US3] Write test: re-entrancy guard — context change during Evaluate defers to single re-evaluate in same file
- [ ] T042 [P] [US3] Write test: auto-evaluate does NOT tick timers (timed objective stays Active without explicit Evaluate(now)) in same file
- [ ] T043 [US3] Run tests — confirm all FAIL

### Implementation

- [ ] T044 [US3] Add EnableAutoEvaluate(), DisableAutoEvaluate(), IsAutoEvaluateEnabled to com.faolline.graphquest/Runtime/QuestEvaluator.cs — subscribe to OnAnyParameterChanged + OnAnyCollectionChanged, re-entrancy guard with dirty flag
- [ ] T045 [US3] Run tests — confirm all PASS
- [ ] T046 [US3] Run full EditMode suite — confirm zero regressions

**Checkpoint**: QuestEvaluator auto-evaluate functional. Quests re-evaluate on context change without polling.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Version bumps, integration validation, cleanup

- [ ] T047 [P] Bump graphcore package.json from 0.19.0 to 0.20.0 in com.faolline.graphcore/package.json
- [ ] T048 [P] Bump graphdialoguesystem package.json from 0.8.0 to 0.9.0 in com.faolline.graphdialoguesystem/package.json
- [ ] T049 [P] Bump graphquest package.json from 0.2.0 to 0.3.0 in com.faolline.graphquest/package.json
- [ ] T050 [P] Update graphcore dependency floor in graphdialoguesystem package.json to 0.20.0
- [ ] T051 [P] Update graphcore dependency floor in graphquest package.json to 0.20.0
- [ ] T052 Run full EditMode suite — confirm all green (final regression check)
- [ ] T053 Validate quickstart.md scenario: PlayDialogueAction on a gameflow node plays dialogue end-to-end headlessly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (uses BaseContext but not wildcard subs directly — DialogueBus uses context.RaiseSignal which already exists). Can start after Phase 2.
- **US2 (Phase 4)**: Depends on Foundational (uses OnAnyParameterChanged for Context Watch refresh)
- **US3 (Phase 5)**: Depends on Foundational (uses OnAnyParameterChanged/CollectionChanged for auto-evaluate)
- **Polish (Phase 6)**: Depends on all stories complete

### User Story Dependencies

- **US1 (PlayDialogueAction)**: Independent — does not need wildcard subs (uses existing context.RaiseSignal). Could technically start in parallel with Phase 2.
- **US2 (Context Watch)**: Needs Phase 2 for event-driven refresh
- **US3 (Auto-Evaluate)**: Needs Phase 2 for wildcard subscriptions

### Parallel Opportunities

- All [P] test tasks within a phase can run in parallel
- US1 can run in parallel with Phase 2 (no dependency on wildcard subs)
- US2 and US3 can run in parallel after Phase 2
- All version bump tasks (T047-T051) can run in parallel

---

## Parallel Example: Phase 2 Tests

```
T002: OnAnyParameterChanged fires on Set<int>
T003: OnAnyParameterChanged fires for every typed Set
T004: OffAnyParameterChanged stops notifications
T005: OnAnyCollectionChanged fires on Add/Remove/Clear
T006: OffAnyCollectionChanged stops notifications
→ All in same file but independent test methods — write in one pass
```

## Parallel Example: US1 Tests

```
T013-T021: All DialogueBus + PlayDialogueAction tests
→ All [P], write in one pass across two test files
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: BaseContext wildcard subs
3. Complete Phase 3: PlayDialogueAction + DialogueBus
4. **STOP and VALIDATE**: Test PlayDialogueAction headlessly
5. This alone eliminates the #1 boilerplate complaint

### Incremental Delivery

1. Phase 2 → Foundation ready
2. US1 → PlayDialogueAction MVP (biggest impact)
3. US2 → Context Watch (debugging)
4. US3 → Auto-evaluate (performance + DX)
5. Phase 6 → Version bumps + validation

---

## Notes

- Constitution IV requires TDD: tests MUST fail before implementation
- graphcore MUST NOT reference graphdialoguesystem or graphquest
- All new public API is MINOR (append-only)
- Editor-only code uses `#if UNITY_EDITOR`
- Run tests via: `Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode`
