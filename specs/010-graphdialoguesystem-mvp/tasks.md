---
description: "Task list for graphdialoguesystem MVP implementation"
---

# Tasks: graphdialoguesystem — Graph-Based Dialogue Library (MVP)

**Input**: Design documents from `specs/010-graphdialoguesystem-mvp/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED. Constitution Principle IV (TDD) is NON-NEGOTIABLE — every behavior gets a failing
EditMode test before its implementation (Red-Green-Refactor). Tests are EditMode-only and headless.

**Organization**: Tasks grouped by user story (US1–US4) for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1, US2, US3, US4 (omitted for Setup, Foundational, Polish)
- All paths are relative to repo root `Assets/FaollineGraphEcosystem/`

## Package root

`com.faolline.graphdialoguesystem/` — Runtime / Localization.Unity / Editor / Tests.EditMode assemblies.
Runtime namespace `Faolline.GraphDialogue`, editor `Faolline.GraphDialogue.Editor`, adapter
`Faolline.GraphDialogue.Localization.Unity`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the package skeleton and assemblies, mirroring `com.faolline.starterGraph`.

- [X] T001 Create package folder `com.faolline.graphdialoguesystem/` with `package.json` (name `com.faolline.graphdialoguesystem`, version `0.1.0`, unity `6000.0`, dependency `com.faolline.graphcore`) mirroring `com.faolline.starterGraph/package.json`
- [X] T002 [P] Create Runtime assembly `com.faolline.graphdialoguesystem/Runtime/com.faolline.graphdialoguesystem.Runtime.asmdef` (rootNamespace `Faolline.GraphDialogue`, references `com.faolline.graphcore.Runtime`, autoReferenced true, NO external deps)
- [X] T003 [P] Create Editor assembly `com.faolline.graphdialoguesystem/Editor/com.faolline.graphdialoguesystem.Editor.asmdef` (rootNamespace `Faolline.GraphDialogue.Editor`, references graphcore.Runtime+Editor and the lib Runtime, Editor platform only, autoReferenced false)
- [X] T004 [P] Create Tests assembly `com.faolline.graphdialoguesystem/Tests/EditMode/com.faolline.graphdialoguesystem.Tests.EditMode.asmdef` (references lib Runtime+Editor, graphcore Runtime+Editor, TestRunner; Editor platform; overrideReferences true + nunit.framework.dll; defineConstraints `UNITY_INCLUDE_TESTS`) mirroring the starterGraph tests asmdef
- [X] T005 [P] Create optional adapter assembly `com.faolline.graphdialoguesystem/Localization.Unity/com.faolline.graphdialoguesystem.Localization.Unity.asmdef` (rootNamespace `Faolline.GraphDialogue.Localization.Unity`, references lib Runtime, references `Unity.Localization`, `versionDefines` mapping `com.unity.localization` → define `GRAPHDIALOGUE_UNITY_LOCALIZATION`, `defineConstraints: ["GRAPHDIALOGUE_UNITY_LOCALIZATION"]` so it compiles only when the package is present)

**Checkpoint**: Empty package compiles with four resolvable assemblies (adapter compiles to nothing without the localization package).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Types every user story depends on — the typed context (Principle VI), the dialogue graph
asset, the localization contract + default provider, and the shared condition/action sets. No story can
start until these exist.

**⚠️ CRITICAL**: Complete before any US phase.

### Tests (write first, must FAIL)

- [X] T006 [P] Write `Tests/EditMode/Runtime/DialogueContextContractTests.cs`: asserts `DialogueContext` typed props (Flag/Counter/Amount/Tag) round-trip via keys, `CreateCloneInstance()` returns `DialogueContext`, and values survive `DeepClone`
- [X] T007 [P] Write `Tests/EditMode/Runtime/DialogueGraphTests.cs`: asserts `DialogueGraph` is a `BaseGraph`, assigns a stable `GraphId`, and round-trips nodes/edges/parameters
- [X] T008 [P] Write `Tests/EditMode/Runtime/CsvLocalizationProviderTests.cs`: asserts CSV parse, `Resolve(key, locale)` per locale, fallback (`#key` + warning) on missing key, locale switch
- [X] T009 [P] Write `Tests/EditMode/Runtime/LocalizationSettingsTests.cs`: asserts safe default provider when unconfigured, `Resolve(key)` uses active provider + locale
- [X] T010 [P] Write `Tests/EditMode/Runtime/ConditionTests.cs`: asserts each condition (AlwaysTrue/False, Bool, Int+operator, Float+operator, String+negate) incl. null-safe false+warning on missing/mistyped key
- [X] T011 [P] Write `Tests/EditMode/Runtime/ActionTests.cs`: asserts Log + SetBool/Int/Float/String write the context via key

### Implementation

- [X] T012 [P] Implement `Runtime/DialogueContextKeys.cs` (const string keys: Flag, Counter, Amount, Tag — the only place literals live)
- [X] T013 Implement `Runtime/DialogueContext.cs` (`BaseContext` subclass; typed Flag/Counter/Amount/Tag via `TryGet`/`Set` + keys; override `CreateCloneInstance()`) — depends on T012
- [X] T014 [P] Implement `Runtime/DialogueGraph.cs` (`BaseGraph` subclass + `[CreateAssetMenu(menuName="GraphDialogue/Dialogue Graph")]`)
- [X] T015 [P] Implement `Runtime/Localization/ILocalizationProvider.cs` (`CurrentLocale`, `Resolve(key, locale)` contract)
- [X] T016 Implement `Runtime/Localization/CsvLocalizationProvider.cs` (default, dependency-free; in-memory key→locale→text; fallback + `[GraphDialogue]` warning) — depends on T015
- [X] T017 Implement `Runtime/Localization/LocalizationSettings.cs` + `Runtime/Localization/LocalizationContext.cs` (active provider+locale, safe default) — depends on T015, T016
- [X] T018 [P] Implement `Runtime/Conditions/ComparisonOperator.cs` (enum Equal/NotEqual/Less/LessOrEqual/Greater/GreaterOrEqual)
- [X] T019 [P] Implement `Runtime/Conditions/AlwaysTrueCondition.cs` and `Runtime/Conditions/AlwaysFalseCondition.cs` (`BaseCondition`, `[CreateAssetMenu]`)
- [X] T020 Implement `Runtime/Conditions/BoolCondition.cs` (`BaseCondition`; ParameterKey+ExpectedValue; null-safe) — depends on T018
- [X] T021 Implement `Runtime/Conditions/IntCondition.cs` and `FloatCondition.cs` (operator compare; null-safe) — depends on T018
- [X] T022 Implement `Runtime/Conditions/StringCondition.cs` (equality+Negate; null-safe) — depends on T018
- [X] T023 [P] Implement `Runtime/Actions/LogAction.cs` (`BaseAction`, logs with `[GraphDialogue]` prefix)
- [X] T024 [P] Implement `Runtime/Actions/SetBoolAction.cs`, `SetIntAction.cs`, `SetFloatAction.cs`, `SetStringAction.cs` (`BaseAction`; ParameterKey+Value → `Set<T>`)

**Checkpoint**: Foundational tests T006–T011 green. Context, graph, localization, conditions/actions ready.

---

## Phase 3: User Story 1 — Author a branching dialogue on the canvas (Priority: P1) 🎯 MVP

**Goal**: A designer builds, connects, edits, saves, and reopens a dialogue graph with start, spoken
lines, a choice (multi-port), sub-dialogue, and ends — deterministic round-trip, multi-window.

**Independent Test**: Create a `DialogueGraph`, add start→line→choice(2 opts)→two ends, set speaker/text
and choice labels, connect, save, reopen → identical structure/ids/order/fields (FR-001..011, SC-002).

### Tests (write first, must FAIL)

- [X] T025 [P] [US1] Write `Tests/EditMode/Runtime/DialogueLineNodeDataTests.cs`: asserts `NodeTypeId == "graphdialogue/line"`, SpeakerKey/TextKey/ExpressionKey (default `neutral`) persist
- [X] T026 [P] [US1] Write `Tests/EditMode/Runtime/DialogueChoiceTests.cs`: asserts `DialogueChoice : BaseChoice` carries `DisplayTextKey`, inherits Id+Condition
- [X] T027 [P] [US1] Write `Tests/EditMode/Editor/DialogueGraphViewAddNodeTests.cs`: asserts context menu adds each of the 5 node types and `CreateNodeView` returns the right view per `NodeType`
- [X] T028 [P] [US1] Write `Tests/EditMode/Editor/ChoiceNodeViewTests.cs`: asserts one output port per choice, `portName == choice.Id`, `RebuildPorts`/`UpdateChoiceLabel`, label = DisplayTextKey/label
- [X] T029 [P] [US1] Write `Tests/EditMode/Editor/DialogueNodeInspectorViewTests.cs`: asserts line speaker/text fields, choice add/remove/label/condition, EndReason, subgraph target, param panel render and mutate the graph
- [X] T030 [P] [US1] Write `Tests/EditMode/Editor/DialogueReloadReconnectTests.cs`: asserts removing one choice preserves surviving option edges (`ReconnectNodeEdges`); LoadGraph loses no data; reopened edges reconnect
- [X] T031 [P] [US1] Write `Tests/EditMode/Editor/DialogueWindowMultiTests.cs`: asserts opening a second asset focuses/creates a separate window without disturbing the first

### Implementation

- [X] T032 [P] [US1] Implement `Runtime/Nodes/DialogueLineNodeData.cs` (`StatementNodeData` subclass; `NodeTypeId`; SpeakerKey/TextKey/ExpressionKey)
- [X] T033 [P] [US1] Implement `Runtime/Choices/DialogueChoice.cs` (`BaseChoice` subclass; DisplayTextKey)
- [X] T034 [P] [US1] Implement `Editor/Edges/DialogueEdgeView.cs` (`BaseEdgeView` subclass)
- [X] T035 [P] [US1] Implement `Editor/Nodes/StartNodeView.cs` and `Editor/Nodes/EndNodeView.cs` (`BaseNodeView`; in/out ports) using `DialogueEdgeView` — depends on T034
- [X] T036 [US1] Implement `Editor/Nodes/DialogueLineNodeView.cs` (`BaseNodeView`; in+out; shows speaker + text key in body) — depends on T032, T034
- [X] T037 [US1] Implement `Editor/Nodes/ChoiceNodeView.cs` (one input; one output per choice, `portName=choice.Id`, label from DisplayTextKey; `RebuildPorts`/`UpdateChoiceLabel`) — depends on T033, T034
- [X] T038 [P] [US1] Implement `Editor/Nodes/SubGraphNodeView.cs` (`BaseNodeView`; in+out; shows target graph name) — depends on T034
- [X] T039 [US1] Implement `Editor/Graph/DialogueGraphView.cs` (`BaseGraphView`; `CreateNodeView` dispatch for 5 types; `CreateEdgeView`→DialogueEdgeView; context menu Add Start/Line/Choice/SubGraph/End; `GetChoiceView`/`RemoveChoiceEdges`; `OnNodeCreated` auto-entry on first Start) — depends on T035, T036, T037, T038
- [X] T040 [US1] Implement `Editor/Inspector/DialogueNodeInspectorView.cs` (`BaseNodeInspectorView`; line speaker/text/expression section, choice add/remove/label/condition with live ports + `ReconnectNodeEdges`, EndReason EnumField, subgraph ObjectField + cycle refusal via `CycleDetector`, typed parameter panel, `AddBaseNodeSection`) — depends on T039
- [X] T041 [US1] Implement `Editor/Window/DialogueGraphEditorWindow.cs` (`BaseGraphEditorWindow`; `CreateGraphView`/`CreateNodeInspectorView`/`OnGraphLoaded`; `[MenuItem]` open; `[OnOpenAsset]` per-asset multi-window) — depends on T039, T040
- [X] T041a [P] [US1] Write `Tests/EditMode/Editor/DialogueEdgeConditionTests.cs`: asserts an edge selected in the inspector can be assigned a `BaseCondition`, the value persists on `BaseEdgeData.Condition`, and a false edge condition blocks traversal at runtime (covers FR-021 "to a connection")
- [X] T041b [US1] Add an edge-condition section to `Editor/Inspector/DialogueNodeInspectorView.cs` (when a single edge is selected, an `ObjectField<BaseCondition>` bound to `BaseEdgeData.Condition`; mark graph dirty) — depends on T040; resolves analyze finding G2
- [X] T041c [US1] Accessibility/contrast pass for the dialogue editor: add a USS stylesheet giving each node type a distinct, sufficiently contrasting color, and on-screen hints for primary actions (toolbar tooltips + a one-line help label); verify keyboard add/select works via the inherited canvas. USS/visual only — validated in quickstart, no logic test. Resolves analyze finding G1 (FR-011)

**Checkpoint**: US1 tests green. A dialogue can be authored (incl. edge conditions), saved, reopened, multi-windowed, with accessible node-type contrast and action hints.

---

## Phase 4: User Story 2 — Play a dialogue back to the end (Priority: P1)

**Goal**: A headless engine plays a dialogue: emits localized line steps, presents choices, accepts a
selection, nests sub-dialogues, refuses cycles, ends once.

**Independent Test**: Build start→line→choice→line→end in memory; start; assert line speaker/text;
advance; assert choice set; choose; advance to end; assert single end event (FR-012..020, SC-003).

### Tests (write first, must FAIL)

- [X] T042 [P] [US2] Write `Tests/EditMode/Runtime/SpeakerTests.cs`: asserts `Speaker` resolves DisplayName via provider with literal fallback, `TryGetExpression` falls back safely
- [X] T043 [P] [US2] Write `Tests/EditMode/Runtime/DialogueLineExecutorTests.cs`: asserts the executor's `NodeType` matches the line node and `Execute` exposes the current line for emission
- [X] T044 [P] [US2] Write `Tests/EditMode/Runtime/DialoguePlayerLineTests.cs`: asserts `Start` emits first `LineStep` with resolved speaker name + text; `Advance` follows the single edge
- [X] T045 [P] [US2] Write `Tests/EditMode/Runtime/DialoguePlayerChoiceTests.cs`: asserts `ChoiceStep` lists options with resolved labels + availability; `Choose(available)` routes that branch; `Choose(unavailable)` no-op
- [X] T046 [P] [US2] Write `Tests/EditMode/Runtime/DialoguePlayerEndTests.cs`: asserts `EndStep` fires once with `EndReason`; terminal/empty-choice cases report stuck; missing entry → diagnostic, no crash
- [X] T047 [P] [US2] Write `Tests/EditMode/Runtime/DialogueSubGraphTests.cs`: asserts sub-dialogue plays and resumes parent on end; cyclic target → `GraphCycleException` before recursion
- [X] T048 [P] [US2] Write `Tests/EditMode/Editor/DialogueWindowExecutionTests.cs`: asserts the window Run/Choose/Continue drain loop pauses at a choice and ends correctly (mirrors starter window tests)

### Implementation

- [X] T049 [P] [US2] Implement `Runtime/Speakers/SpeakerExpression.cs` (`[Serializable]`; Key + Asset) and `Runtime/Speakers/Speaker.cs` (`ScriptableObject`; SpeakerId, DisplayNameKey, DisplayNameFallback, Expressions, FallbackExpression, `TryGetExpression`)
- [X] T050 [P] [US2] Implement `Runtime/Playback/DialogueStep.cs` (abstract `DialogueStep` + `LineStep`/`EndStep`) and `Runtime/Playback/ChoiceOption.cs` + `ChoiceStep.cs`
- [X] T051 [US2] Implement `Runtime/Execution/DialogueLineExecutor.cs` (`INodeExecutor` for `DialogueLineNodeData.NodeTypeId`; exposes line speaker/text) — depends on T032
- [X] T052 [US2] Implement `Runtime/Execution/DialogueExecutorRegistryFactory.cs` (builds `NodeExecutorRegistry` with the line executor) — depends on T051
- [X] T053 [US2] Implement `Runtime/Playback/DialoguePlayer.cs` (wraps `BaseRunner`; ctor graph+context+provider+speakerLookup; Start/Advance/Choose/Back/BackToCheckpoint; OnLine/OnChoices/OnEnded/OnStuck; resolves text+speaker via provider; choice availability via condition eval) — depends on T013, T016, T049, T050, T052
- [X] T054 [US2] Implement `Editor/Window/DialogueGraphEditorWindow.cs` execution path: Run uses `DialogueContext` + registry factory + active provider; drain loop pausing at `ChoiceNodeData`; Choose/Continue/GoBack/Checkpoint (extends T041) — depends on T041, T053

**Checkpoint**: US2 tests green. A dialogue plays start→end headlessly and via the window.

---

## Phase 5: User Story 3 — Inline conditions and effects (Priority: P2)

**Goal**: Gated options and entry conditions hide/show branches; enter/exit effects mutate shared state
later conditions read; step-back restores state.

**Independent Test**: Choice with one open + one gated option; gated unavailable while false; effect
flips it true; on return the option becomes available; step-back restores (FR-021..027, SC-005/006).

### Tests (write first, must FAIL)

- [X] T055 [P] [US3] Write `Tests/EditMode/Runtime/InlineConditionPlaybackTests.cs`: asserts a gated `DialogueChoice.Condition` toggles `ChoiceOption.Available`; a failing `EntryConditions` reports stuck without presenting the node
- [X] T056 [P] [US3] Write `Tests/EditMode/Runtime/InlineEffectPlaybackTests.cs`: asserts `OnEnterActions` set state before the step is emitted and `OnExitActions` run before advancing; later conditions read the new value
- [X] T057 [P] [US3] Write `Tests/EditMode/Runtime/DialoguePlayerHistoryTests.cs`: asserts `Back()` restores prior context values and `BackToCheckpoint()` returns to the nearest checkpoint node

### Implementation

- [X] T058 [US3] Wire condition evaluation for choice availability and entry/edge gating through `DialoguePlayer` (ensure parity with `BaseRunner` semantics; null-safe) — depends on T053; verifies T020–T022 integrate
- [X] T059 [US3] Wire enter/exit effect execution + `Back`/`BackToCheckpoint` snapshot restoration through `DialoguePlayer` (delegates to `BaseRunner` history; `DialogueContext` round-trips) — depends on T053
- [X] T060 [P] [US3] Add the inline condition/effect editing surface in `Editor/Inspector/DialogueNodeInspectorView.cs` (entry conditions, enter/exit actions, per-choice condition already present) — depends on T040

**Checkpoint**: US3 tests green. Reactivity works end to end with step-back.

---

## Phase 6: User Story 4 — Localize across providers (Priority: P2)

**Goal**: The same dialogue plays in multiple languages by switching locale; works with the default CSV
provider and the optional Unity Localization adapter, no graph edits.

**Independent Test**: Dialogue with text keys translated in 2 locales via CSV; play per locale, assert
text; swap to the Unity adapter, assert same graph resolves through it (FR-028..033, SC-004/007).

### Tests (write first, must FAIL)

- [X] T061 [P] [US4] Write `Tests/EditMode/Runtime/LocalizedPlaybackTests.cs`: asserts `LineStep.ResolvedText` and `ChoiceOption.ResolvedLabel` match the active locale across two languages with no graph change; speaker display name localized
- [X] T062 [P] [US4] Write `Tests/EditMode/Runtime/LocalizationFallbackTests.cs`: asserts a key missing in the active locale yields the defined fallback + warning, never empty
- [X] T063 [P] [US4] Write `Tests/EditMode/Runtime/ProviderSwapTests.cs`: asserts the same graph resolves through a stub "engine" provider and the CSV provider identically (adapter contract; runs without `com.unity.localization` via a stand-in implementing `ILocalizationProvider`)

### Implementation

- [X] T064 [US4] Verify/extend `DialoguePlayer` resolves all author-facing text (line, choice label, speaker name) through the injected provider + active locale (extends T053) — depends on T053
- [X] T065 [P] [US4] Implement `Localization.Unity/UnityLocalizationProvider.cs` (`ILocalizationProvider` over `com.unity.localization` String Tables; guarded by `GRAPHDIALOGUE_UNITY_LOCALIZATION`) — depends on T005, T015
- [X] T066 [US4] Add a lightweight provider/locale selection surface (settings asset or window field) wiring `LocalizationSettings` into the window Run path — depends on T017, T054

**Checkpoint**: US4 tests green. Same dialogue plays in 2 locales through both providers.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T067 [P] Implement `Editor/Samples/DialogueSampleBuilder.cs` (menu builds a parent + child `DialogueGraph` with speakers, a gated choice, inline conditions/actions, a checkpoint, a sub-dialogue, typed parameters, and a 2-locale CSV table)
- [X] T068 [P] Write `Tests/EditMode/Editor/DialogueSampleIntegrationTests.cs`: builds the sample and plays it start→end in two locales headlessly (covers SC-001/003/004 end to end)
- [X] T069 [P] Add `com.faolline.graphdialoguesystem/README.md` documenting the public API (from contracts/public-api.md), the localization providers, and the inline-only reactivity model
- [ ] T070 Run `quickstart.md` validation manually in the editor; confirm `git diff` shows zero changes under `com.faolline.graphcore/` (SC-008)
- [ ] T071 Final full EditMode suite run — all green (SC-009); confirm no `[GraphDialogue]`/`[GraphCore]` errors in console, warnings justified

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: no deps.
- **Foundational (P2)**: after Setup. BLOCKS all user stories.
- **US1 (P3)**: after Foundational. The authoring MVP.
- **US2 (P4)**: after Foundational. Uses US1's node/choice types (T032/T033) but is independently testable headlessly.
- **US3 (P5)**: after US2 (extends `DialoguePlayer`).
- **US4 (P6)**: after US2 (extends `DialoguePlayer` text resolution).
- **Polish (P7)**: after the desired stories.

### Story Independence

- US1 is fully independent (authoring only).
- US2 depends on the two domain data types from US1 (line node, choice) — sequence US1 → US2, or build T032/T033 first if parallelizing.
- US3 and US4 each extend the US2 player and can be done in either order; both independently testable.

### Within Each Story

- Tests first, confirm FAIL, then implement (Constitution IV).
- Data types → views → graph view → inspector → window.
- Player core → wiring (US3/US4).

### Parallel Opportunities

- Setup: T002–T005 in parallel.
- Foundational: tests T006–T011 in parallel; impl T012/T014/T015/T018/T019/T023/T024 in parallel.
- US1: tests T025–T031 in parallel; data types T032–T034 in parallel; node views T035/T038 in parallel.
- US2: tests T042–T048 in parallel; T049/T050 in parallel.
- US3/US4: their test files are all [P]; the two stories can run in parallel by different devs.

---

## Parallel Example: User Story 1

```text
# Tests first (all parallel):
T025 DialogueLineNodeDataTests, T026 DialogueChoiceTests, T027 GraphViewAddNodeTests,
T028 ChoiceNodeViewTests, T029 InspectorViewTests, T030 ReloadReconnectTests, T031 WindowMultiTests

# Then data types (parallel):
T032 DialogueLineNodeData, T033 DialogueChoice, T034 DialogueEdgeView
```

---

## Implementation Strategy

### MVP First

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1 (authoring) → Phase 4 US2 (playback).
2. **STOP and VALIDATE**: a dialogue can be authored, saved, reopened, and played start→end in one
   language, headlessly. That is the demonstrable MVP (US1+US2, both P1).

### Incremental Delivery

3. Add US3 (reactivity) → validate gating + step-back.
4. Add US4 (localization across providers) → validate two locales + both providers.
5. Polish: sample builder, README, quickstart validation, full suite green.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Every implementation task is preceded by a failing test (Constitution IV, NON-NEGOTIABLE).
- Tests are EditMode-only and headless (project memory: maximize headless testing).
- Zero graphcore modification (Constitution I); verify with `git diff` at T070.
- `com.unity.localization` stays isolated in the adapter assembly (Constitution v1.2.0 Dependencies).
- Commit after each task or logical group (no Co-Authored-By trailer — project convention).
