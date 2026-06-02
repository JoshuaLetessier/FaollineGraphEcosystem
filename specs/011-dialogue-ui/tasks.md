---
description: "Task list for In-Game Dialogue UI"
---

# Tasks: In-Game Dialogue UI

**Input**: Design documents from `specs/011-dialogue-ui/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contract.md

**Tests**: INCLUDED — the constitution mandates TDD (Principle IV). EditMode tests cover the logic
seams (driver routing, choice-by-id, avatar resolution, input mapping). Pixel-level rendering is
validated via the samples (US5) / manual play.

**Organization**: By user story (priority order). Each story is an independently testable increment.

**Base paths**:
- Runtime: `com.faolline.graphdialoguesystem/UI/Runtime/`
- Tests: `com.faolline.graphdialoguesystem/UI/Tests/EditMode/`
- Samples: `com.faolline.graphdialoguesystem/Samples~/DialogueUI/`
- Namespace: `Faolline.GraphDialogue.UI`

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Create `com.faolline.graphdialoguesystem/UI/Runtime/com.faolline.graphdialoguesystem.UI.asmdef` referencing `com.faolline.graphdialoguesystem.Runtime`, `com.faolline.graphcore.Runtime`, `Unity.TextMeshPro`, `Unity.InputSystem`; `autoReferenced: true`, no platform restriction.
- [ ] T002 [P] Create `com.faolline.graphdialoguesystem/UI/Tests/EditMode/com.faolline.graphdialoguesystem.UI.Tests.EditMode.asmdef` (Editor-only, references UI.Runtime + graphdialoguesystem.Runtime + graphcore.Runtime + UnityEngine.TestRunner + UnityEditor.TestRunner + nunit, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: Completed before any user story. Delivers the contract + the tested headless driver.

- [ ] T003 Define `IDialogueView` in `UI/Runtime/IDialogueView.cs` per contracts/ui-contract.md (BindSpeakers, ShowLine, ShowChoices, HideAll, `event Action<string> ChoiceSelected`).
- [ ] T004 [P] Create test double `RecordingDialogueView` in `UI/Tests/EditMode/RecordingDialogueView.cs` — implements `IDialogueView`, records last LineStep/ChoiceStep/HideAll calls and exposes a method to raise `ChoiceSelected`.
- [ ] T005 Create `DialogueViewBase` (abstract MonoBehaviour) in `UI/Runtime/DialogueViewBase.cs` — speaker registry via `BindSpeakers` (index by `Speaker.SpeakerId`), abstract `ShowLine`/`ShowChoices`/`HideAll`, `event Action<string> ChoiceSelected` with a protected `RaiseChoiceSelected(id)`. (Avatar logic deferred to US3.)
- [ ] T006 Write FAILING EditMode tests `UI/Tests/EditMode/DialogueDriverRoutingTests.cs`: real `DialoguePlayer` over an in-memory graph + `RecordingDialogueView` assert — line→ShowLine(resolved text), choices→ShowChoices(ids/availability), end→HideAll, `ChoiceSelected(id)`→player routes to that branch, `Advance()` no-op during choices, null-view runs + logs warning.
- [ ] T007 Implement `DialogueDriver` (MonoBehaviour) in `UI/Runtime/DialogueDriver.cs` to pass T006: build/own `DialoguePlayer` from `graph`+`DialogueContext`+`LocalizationContext.Current` provider+speaker lookup; subscribe player events → view; subscribe `view.ChoiceSelected` → `Choose`; public `StartDialogue/Advance/Choose/Back/BackToCheckpoint`; null-view safe; unsubscribe + `HideAll` on disable/destroy; clear state on restart. (No keyboard input yet — US4.)

**Checkpoint**: Headless driver is green; views can now be built against the contract.

---

## Phase 3: User Story 1 - Play a dialogue on a Canvas UI (Priority: P1) 🎯 MVP

**Goal**: Text + speaker + clickable choices on UGUI/TMP; routing by id; hide on end.

**Independent Test**: Drive a `CanvasDialogueView` with the driver; advance a line, click a choice, reach end — text shown, routing correct, UI cleared.

- [ ] T008 [P] [US1] Write FAILING EditMode tests `UI/Tests/EditMode/CanvasDialogueViewTests.cs`: construct a `CanvasDialogueView` with TMP texts + buttons; `ShowLine` sets line/speaker text; `ShowChoices` enables exactly N buttons with correct labels and `interactable == Available`; invoking a button's `onClick` raises `ChoiceSelected` with that option's `ChoiceId`; `HideAll` clears texts and hides buttons.
- [ ] T009 [US1] Implement `CanvasDialogueView : DialogueViewBase, IDialogueView` in `UI/Runtime/CanvasDialogueView.cs` to pass T008 (serialized `TMP_Text` line/speaker, `choicesContainer`, `List<Button>` with child `TMP_Text`; surplus options hidden + `[GraphDialogue]` warning; pointer-click advance hook).

**Checkpoint**: Canvas dialogue is playable end-to-end (text + choices). MVP shippable.

---

## Phase 4: User Story 2 - Play the same dialogue on a UI Toolkit UI (Priority: P2)

**Goal**: Same behaviour rendered through a `UIDocument`; Dynamic and Slots choice modes.

**Independent Test**: Swap the driver's view to `UIToolkitDialogueView`; same outcomes as US1.

- [ ] T010 [P] [US2] Write EditMode tests `UI/Tests/EditMode/UIToolkitDialogueViewTests.cs` for the choice-mode logic that is panel-independent (Dynamic builds N buttons in a detached `VisualElement` container; Slots enables present / disables absent; `clicked` raises `ChoiceSelected(id)`; disabled when `!Available`). Use a manually-built `VisualElement` tree where a `UIDocument` is not required.
- [ ] T011 [US2] Implement `UIToolkitDialogueView : DialogueViewBase, IDialogueView` in `UI/Runtime/UIToolkitDialogueView.cs` to pass T010 (serialized `UIDocument` + element names + `ChoiceDisplayMode {Dynamic,Slots}` + slot prefix/max; lazy element binding; line/speaker `Label`s; disabled USS class for unavailable options).

**Checkpoint**: Both front-ends interchangeable on the same driver (SC-002).

---

## Phase 5: User Story 3 - Speaker avatars (Priority: P2)

**Goal**: Avatar reflects current speaker+expression, swaps on change, clears on hide, degrades gracefully.

**Independent Test**: Play alternating-speaker lines; correct avatar per line; unknown speaker → no avatar/no error; none left after hide.

- [ ] T012 [P] [US3] Write FAILING EditMode tests `UI/Tests/EditMode/AvatarLifecycleTests.cs`: on a `DialogueViewBase` subclass, `RequestAvatarSwap(speakerId, expr)` resolves via `Speaker.TryGetExpression` (instantiates under a temp current root), swapping demotes the prior to the previous root, unknown speaker/expression spawns nothing and throws nothing (uses `FallbackExpression` when set), and `ClearAvatarsOnHide` leaves no instances.
- [ ] T013 [US3] Add the avatar lifecycle to `UI/Runtime/DialogueViewBase.cs` to pass T012 (current/previous mounts, `destroyAvatarOnHide`, optional transition, spawn/demote/despawn coroutines, `RequestAvatarSwap`, `ClearAvatarsOnHide`, avatar events).
- [ ] T014 [P] [US3] Create abstract `AvatarTransition` MonoBehaviour in `UI/Runtime/AvatarTransition.cs` (`IEnumerator Spawn/Despawn/DemoteToPrevious`).
- [ ] T015 [US3] Wire avatars into the views: call `RequestAvatarSwap(step.SpeakerId, step.ExpressionKey)` in `CanvasDialogueView.ShowLine` and `UIToolkitDialogueView.ShowLine`; call `ClearAvatarsOnHide()` in both `HideAll`.

**Checkpoint**: Avatars work in both front-ends.

---

## Phase 6: User Story 4 - Keyboard input (Priority: P3)

**Goal**: Space = advance line; 1–9 = choose; both input backends; clicks unaffected.

**Independent Test**: With either backend, Space advances, number key chooses; unavailable/absent number is a no-op.

- [ ] T016 [P] [US4] Write FAILING EditMode tests `UI/Tests/EditMode/DialogueDriverInputTests.cs` for the input→action mapping seam: a "choose index k" call selects the k-th currently-displayed available option's id (and is a no-op when absent/unavailable); an "advance" call is ignored during a choice step. (Test the internal mapping methods, not raw key polling.)
- [ ] T017 [US4] Add keyboard handling to `UI/Runtime/DialogueDriver.cs` (`Update`: Space→Advance when on a line; digits/numpad 1–9→choose the k-th option) behind `#if ENABLE_INPUT_SYSTEM` / `#else ENABLE_LEGACY_INPUT_MANAGER`, routing through the seam validated in T016.

**Checkpoint**: Keyboard parity with the reference; clicks still work.

---

## Phase 7: User Story 5 - Samples (Priority: P3)

**Goal**: Runnable Canvas and UI Toolkit demos wired to the existing sample dialogue.

**Independent Test**: Open each sample, press Play → dialogue runs to completion, no manual wiring.

- [ ] T018 [P] [US5] Canvas sample (scene + prefab) in `com.faolline.graphdialoguesystem/Samples~/DialogueUI/Canvas/` wired to the generated sample dialogue + speakers + a `DialogueDriver` + `CanvasDialogueView`.
- [ ] T019 [P] [US5] UI Toolkit sample (UXML + USS + scene/prefab) in `com.faolline.graphdialoguesystem/Samples~/DialogueUI/UIToolkit/` with `line-text`/`speaker-name`/`choices-container` + `DialogueDriver` + `UIToolkitDialogueView`.

**Checkpoint**: SC-001 met for both front-ends.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T020 [P] Add a "Showing dialogue in-game (Canvas / UI Toolkit)" section to `specs/010-graphdialoguesystem-mvp/authoring-guide.md` (link the quickstart).
- [ ] T021 [P] Ensure XML `<summary>` docs on all public UI types/members; `[GraphDialogue]` prefix on all logs.
- [ ] T022 Verify the headless core still compiles with the UI assembly removed (no UI dependency leak — SC-007).
- [ ] T023 Run `specs/011-dialogue-ui/quickstart.md` validation in the editor (both paths) and fix any wiring gaps.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** blocks everything.
- **US1 (P3 phase)** depends only on Foundational → MVP.
- **US2** depends on Foundational (reuses the driver; independently testable by swapping the view).
- **US3** depends on Foundational; modifies `DialogueViewBase` + both views' `ShowLine` (so best done after US1/US2 exist, but avatar base+tests can start right after Foundational).
- **US4** depends on Foundational (extends the driver); independent of the views.
- **US5** depends on US1/US2 (and US3 for avatar demo).
- **Polish** last.

### Within each story
- Tests first (write → fail → implement → pass), per constitution IV.

### Parallel opportunities
- T001 / T002 setup in parallel.
- T004 (test double) parallel with T003/T005.
- US2 and US4 can proceed in parallel with US3 (different files: UIToolkit view / driver input / view base).
- T018 / T019 samples in parallel; T020 / T021 polish in parallel.

---

## Implementation Strategy

### MVP (stop-and-validate)
1. Phase 1 Setup → 2. Phase 2 Foundational (tested driver) → 3. Phase 3 US1 Canvas → **validate a dialogue plays on Canvas**. Ship.

### Incremental
US2 (UI Toolkit) → US3 (avatars) → US4 (keyboard) → US5 (samples) → Polish. Each adds value without breaking prior stories.

---

## Notes
- [P] = different files, no incomplete dependency.
- Each story independently testable; commit per task or logical group.
- Verify EditMode tests fail before implementing (TDD).
- Editor is open during dev → run tests via the Test Runner; no batchmode.
