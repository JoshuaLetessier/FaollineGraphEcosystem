---
description: "Task list for 028-dialogue-render-bridge (runner-agnostic DialoguePresenter + driver ChooseById/choice-pause)"
---

# Tasks: dialogue render bridge (slice 9)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD), EditMode. Batchmode (no `-quit`; re-run after a source change; verify XML). Branch
`028-dialogue-render-bridge` (stacks on master). **graphcore UNTOUCHED; gameflow ⊥ dialoguesystem (no cross-lib
dep added).** dialoguesystem `0.2.0 → 0.3.0`, gameflow `0.5.0 → 0.6.0`, both append-only.

## Phase 1: US1 — runner-agnostic presenter (Priority: P1) 🎯 MVP

**Goal**: resolve a dialogue line/choice node owned by ANY runner into a `LineStep`/`ChoiceStep`.

**Independent test**: drive a `DialogueGraph` with a plain `BaseRunner`; `presenter.ResolveLine`/`ResolveChoice`
of its current node yield the resolved step; a non-dialogue node ⇒ `Resolve` returns null.

- [X] T001 [P] [US1] New dialoguesystem EditMode test (e.g. `Tests/EditMode/Playback/DialoguePresenterTests.cs`): build a small `DialogueGraph` (a `Speaker`, a `DialogueLineNodeData`, a `ChoiceNodeData` with two `DialogueChoice`s, one condition-gated) + a stub `ILocalizationProvider`; drive it with a plain `BaseRunner` (NOT a `DialoguePlayer`); assert `presenter.ResolveLine(currentLine, ctx)` → `LineStep` with resolved speaker name + localized text, and `presenter.ResolveChoice(currentChoice, ctx)` → `ChoiceStep` whose options carry label + availability (the gated one unavailable when its condition is false); `presenter.Resolve(startNode, ctx)` → null. Confirm RED (`DialoguePresenter` missing).
- [X] T002 [US1] Implement `com.faolline.graphdialoguesystem/Runtime/Playback/DialoguePresenter.cs`: ctor `(ILocalizationProvider, ILocalizedAssetProvider=null, Func<string,Speaker>=null, LocalizationStrictMode=Permissive)`; `ResolveLine`/`ResolveChoice`/`Resolve(BaseNodeData,BaseContext)`; `MissingKeys`/`OnMissingKey`; port `ResolveChecked`/`ResolveSpeakerName`/text interpolation + voice-asset resolution from `DialoguePlayer`. `[GraphDialogue]` prefix, XML docs. Confirm T001 GREEN.

## Phase 2: US2 — DialoguePlayer delegates, unchanged behavior (Priority: P1)

- [X] T003 [US2] Refactor `com.faolline.graphdialoguesystem/Runtime/Playback/DialoguePlayer.cs` to build an internal `DialoguePresenter` from its ctor args and route `BuildLineStep`/`BuildChoiceStep`/missing-key handling through it (forward `OnMissingKey`; `MissingKeys` reads the presenter). NO public API change. Confirm the **existing** dialogue playback suite stays GREEN (the regression guard for US2).

## Phase 3: US3 — driver choose + choice-pause (Priority: P1)

**Goal**: under AutoAdvance, a choice pauses for `ChooseById`; non-choice chains unchanged.

- [X] T004 [P] [US3] Add to `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: a graph Start → `ChoiceNodeData` (two branches) under `AutoAdvance = true` ⇒ on entering the choice the driver does NOT advance (still on the choice / not Ended); `driver.ChooseById(branchId)` ⇒ advances along that branch; assert the existing non-choice `AutoAdvance_RunsChainToEnd` semantics still hold (add a sibling assertion if useful). Confirm RED (`ChooseById` missing / choice auto-advances).
- [X] T005 [US3] In `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs`: `HandleNodeCompleted` → `if (_autoAdvance && !(node is ChoiceNodeData)) _runner.Proceed();`; add `public void ChooseById(string id)` → guarded `_runner.ChooseById(id)` (mirrors `Advance`). XML docs; `[GraphGameFlow]`. Confirm T004 GREEN.

## Phase 4: Polish

- [X] T006 Run the ENTIRE suite via batchmode: graphcore + graphstandard + dialoguesystem + gameflow EditMode all green, AND PlayMode green (graphcore untouched). Record totals.
- [X] T007 [P] Bump `com.faolline.graphdialoguesystem/package.json` `0.2.0 → 0.3.0` (fix the stale README version header `0.1.0 → 0.3.0`) + CHANGELOG + a "host-render via DialoguePresenter" note; bump `com.faolline.graphgameflow/package.json` `0.5.0 → 0.6.0` + README/CHANGELOG (`ChooseById` + AutoAdvance choice-pause).
- [X] T008 [P] Verify append-only + layering: `DialoguePlayer` public API unchanged; no dependency added between gameflow and dialoguesystem; graphcore untouched; XML docs + prefixes on every new member.

## Dependencies

- **T001 → T002 → T003** (presenter, then player delegates). **T004 → T005** (driver). Phases independent of each
  other except both land before Polish (T006–T008).

## Implementation strategy

- The presenter is a behavior-preserving extraction of the player's resolution; the player delegating to it is
  the regression-guarded refactor. The driver gains one guard (`!(node is ChoiceNodeData)`) + one re-exposed
  method. The consumer composes the two — neither lib depends on the other.
