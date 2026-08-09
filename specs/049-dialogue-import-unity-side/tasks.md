---
description: "Task list for feature implementation"
---

# Tasks: Dialogue Graph Generation from a Pivot Interchange Format

**Input**: Design documents from `/specs/049-dialogue-import-unity-side/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/additions.md, quickstart.md

**Tests**: Included and REQUIRED (constitution IV, same as 048) — every implementation task lists the test task(s) it depends on.

**Organization**: Tasks are grouped by user story (spec.md priorities).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Maps to US1–US3 from spec.md
- Paths are relative to the repo root; two packages are touched (`com.faolline.graphimport/`, `com.faolline.graphdialoguesystem/`)

---

## Phase 1: Setup

- [ ] T001 Add `com.faolline.graphdialoguesystem` (0.17.2) as a dependency in `com.faolline.graphimport/package.json`; add `com.faolline.graphdialoguesystem.Runtime` to the `references` array of `com.faolline.graphimport/Editor/com.faolline.graphimport.Editor.asmdef` and `com.faolline.graphimport/Tests/EditMode/com.faolline.graphimport.Tests.EditMode.asmdef`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared plumbing every user story builds on — the plan-entry kind, the shared resolver seam, and the raw pivot types

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Add `DialogueAsset` to the `PlanEntryKind` enum in `com.faolline.graphimport/Runtime/Planning/PlanEntryKind.cs`
- [ ] T003 [P] Implement `IProjectAssetResolver` (`ResolveGraph`, `ResolveSpeaker`) and `NullProjectAssetResolver` in `com.faolline.graphimport/Editor/Resolution/IProjectAssetResolver.cs`
- [ ] T004 Retrofit `FlowAssetGenerator` (`com.faolline.graphimport/Editor/Generation/FlowAssetGenerator.cs`) to take an `IProjectAssetResolver` in its constructor instead of `Func<PivotReference, BaseGraph>`, calling `resolver.ResolveGraph(step.ContentRef.TargetTable, step.ContentRef.TargetId)`; update `GraphImportWindow`'s default `Generators` wiring and any existing `AssetGenerationTests`/`PlanApplierTests` call sites that construct it directly (depends on: T003)
- [ ] T005 [P] Implement `InterchangeDialogueSet`/`InterchangeDialogue`/`InterchangeNode` (raw JSON shape, `LoadFromJson`) in `com.faolline.graphimport/Runtime/DialoguePivot/InterchangeDialogueSet.cs`
- [ ] T006 [P] Implement `PivotDialogue`, `PivotDialogueNode` (+ `PivotLine`/`PivotChoice`/`PivotChoiceOption`/`PivotEnd`/`PivotSubDialogueLink`) in `com.faolline.graphimport/Runtime/DialoguePivot/PivotDialogue.cs` and `PivotDialogueNode.cs`

**Checkpoint**: plan-entry kind, shared resolver, and pivot types exist — user story work can begin

---

## Phase 3: User Story 1 - Generate a playable dialogue from the interchange format (Priority: P1) 🎯 MVP

**Goal**: A self-contained interchange file (lines, a choice, an ending) produces a real, playable dialogue graph with correctly-positioned localization text.

**Independent Test**: Feed a one-dialogue interchange file (opening line → choice with two options → two follow-up lines → two endings) and verify the generated graph reproduces that exact structure, with line text positioned for automatic localization pickup.

### Tests for User Story 1 ⚠️ write first, confirm they fail

- [ ] T007 [P] [US1] Write failing tests for `DialoguePivotBuilder` (line/choice/end only, no sub-dialogue) in `com.faolline.graphimport/Tests/EditMode/DialoguePivotBuilderTests.cs`: correct `PivotDialogue` from a valid interchange; a dangling `Next` reference throws with dialogue/node context; a duplicate node id throws; an `EntryNodeId` not matching any node throws (FR-002, FR-006)
- [ ] T008 [P] [US1] Write failing tests for `DialogueAssetGenerator` (line/choice/end only) in `com.faolline.graphimport/Tests/EditMode/DialogueAssetGenerationTests.cs`: generates a `DialogueGraph` asset whose node order/choice options/ending match the pivot; each line's `SpeakerKey` is copied through; each line's text lands on `Title` (SC-001, SC-002 groundwork)

### Implementation for User Story 1

- [ ] T009 [US1] Implement `DialoguePivotBuilder.Build` for line/choice/end node kinds, with the FR-002/FR-006 validations, in `com.faolline.graphimport/Runtime/DialoguePivot/DialoguePivotBuilder.cs` (depends on: T005, T006, T007)
- [ ] T010 [US1] Implement `DialogueAssetGenerator` for line/choice/end node kinds (via `DialogueGraphBuilder.AddLine`/`AddChoice`+`Option`/`AddEnd`, best-effort speaker resolution + `WithSpeaker` via `IProjectAssetResolver.ResolveSpeaker`) in `com.faolline.graphimport/Editor/Generation/DialogueAssetGenerator.cs` (depends on: T003, T009, T008)
- [ ] T011 [P] [US1] Add a hand-authored interchange JSON fixture (line → choice → two endings, no sub-dialogue) under `com.faolline.graphimport/Samples/DialogueExample/`

**Checkpoint**: US1 independently testable and demoable — a self-contained dialogue generates correctly, MVP delivered.

---

## Phase 4: User Story 2 - One dialogue jumps into another (Priority: P1)

**Goal**: A node in one dialogue can reference a second, separately-defined dialogue, producing a real link (not a copy) between their generated assets.

**Independent Test**: An interchange file with two dialogues, one containing a node that references the other by id or name; verify the generated first dialogue's graph links to the second dialogue's own asset.

### Tests for User Story 2 ⚠️ write first, confirm they fail

- [ ] T012 [P] [US2] Write failing tests for the new `DialogueGraphBuilder.AddSubGraph` in `com.faolline.graphdialoguesystem/Tests/EditMode/DialogueGraphBuilderSubGraphTests.cs`: adds a `SubGraphNodeData` node with `TargetGraph` set, wireable via the existing `.To(...)` edge machinery like any other handle
- [ ] T013 [P] [US2] Write failing tests for `DialoguePivotBuilder`'s sub-dialogue handling in `DialoguePivotBuilderTests.cs`: `TargetDialogueRef` resolves by id-or-name against the full `InterchangeDialogueSet`; a reference cycle across two or more dialogues throws a specific, identifiable error (FR-007)
- [ ] T014 [P] [US2] Write failing tests for `DialogueAssetGenerator`'s sub-dialogue node handling in `DialogueAssetGenerationTests.cs`: an unresolved target produces a `SubGraph` node with a null `TargetGraph` (not an exception, FR-005); a resolved target produces one with `TargetGraph` set to the real asset

### Implementation for User Story 2

- [ ] T015 [US2] Implement `DialogueGraphBuilder.AddSubGraph(title, target)` + `DialogueSubGraphHandle` in `com.faolline.graphdialoguesystem/Runtime/Builder/DialogueGraphBuilder.cs` and `DialogueSubGraphHandle.cs` (depends on: T012)
- [ ] T016 [US2] Extend `DialoguePivotBuilder` to resolve `PivotSubDialogueLink.TargetDialogueRef` across the full `InterchangeDialogueSet` by id-or-name, and to detect reference cycles via DFS before returning any dialogue (depends on: T009, T013)
- [ ] T017 [US2] Extend `DialogueAssetGenerator` to handle `PivotSubDialogueLink` nodes via the new `AddSubGraph` + `IProjectAssetResolver.ResolveGraph` (depends on: T010, T015, T014)
- [ ] T018 [P] [US2] Extend `Samples/DialogueExample/` with a second dialogue and a sub-dialogue link from the first into it

**Checkpoint**: US2 independently testable and demoable — a real dialogue-to-dialogue jump works, unresolved links degrade to a documented-safe incomplete state rather than a crash.

---

## Phase 5: User Story 3 - Dialogue assets go through the same safe review/apply pipeline (Priority: P2)

**Goal**: Dialogue generation is not a parallel pipeline — it produces plan entries and conflict handling indistinguishable in kind from quest/flow assets.

**Independent Test**: A run mixing quest/flow and dialogue data; confirm the dialogue asset appears in the same preview and is subject to the same collision handling as everything else.

### Tests for User Story 3 ⚠️ write first, confirm they fail

- [ ] T019 [P] [US3] Write failing tests for dialogue plan-entry generation (extend `com.faolline.graphimport/Tests/EditMode/PlanBuilderTests.cs` or add a new file) — one `DialogueAsset` `PlanEntry` per `PivotDialogue`, deterministic across repeated runs, same as quest/flow entries (FR-008)
- [ ] T020 [P] [US3] Write failing tests confirming a colliding dialogue asset is reported through the exact same `ConflictReport`/`PlanApplier` path already covered by `PlanConflictDetectorTests.cs`/`PlanApplierTests.cs` — no dialogue-specific conflict mechanism (FR-008, FR-009)

### Implementation for User Story 3

- [ ] T021 [US3] Extend plan-building to emit `PlanEntryKind.DialogueAsset` entries from a `PivotDialogue` list (extend `PlanBuilder` in `com.faolline.graphimport/Runtime/Planning/PlanBuilder.cs`, or add a sibling method if quest and dialogue plan inputs don't naturally share one call signature — see research.md §2 on why the two pivots don't share a base type) (depends on: T006, T019)
- [ ] T022 [US3] Wire `DialogueAssetGenerator` into the `IReadOnlyDictionary<PlanEntryKind, IAssetGenerator>` used by `GraphImportWindow` and any CI/pipeline entry point, alongside the existing quest/flow generators (depends on: T010, T021, T020)

**Checkpoint**: All three user stories independently functional — dialogue generation is a first-class, safety-equivalent citizen of the existing pipeline.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T023 [P] Run `quickstart.md`'s walkthrough end-to-end against `Samples/DialogueExample/`, headlessly
- [ ] T024 [P] Add XML `<summary>` documentation to every new public type in both touched packages
- [ ] T025 Run the full `com.faolline.graphimport` + `com.faolline.graphdialoguesystem` EditMode suites together (batchmode) to confirm zero collateral impact from the `AddSubGraph` addition and the `FlowAssetGenerator` retrofit
- [ ] T026 Bump `com.faolline.graphimport`'s `package.json` version (new capability + new dependency) and `com.faolline.graphdialoguesystem`'s (new additive builder method), each with a semver rationale note

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: depends on Foundational only — fully self-contained, no sub-dialogue involvement
- **US2 (Phase 4)**: depends on Foundational + US1's `DialoguePivotBuilder`/`DialogueAssetGenerator` (T009, T010), which it extends rather than duplicates
- **US3 (Phase 5)**: depends on US1 (needs `PivotDialogue`s to plan over) but not on US2 — a plan can be built and applied for sub-dialogue-free dialogues alone
- **Polish (Phase 6)**: depends on all three stories

### Parallel Opportunities

- T002, T003, T005, T006 (Phase 2) in parallel
- T007, T008 (US1 tests) in parallel; T011 in parallel with T009/T010
- T012, T013, T014 (US2 tests) in parallel
- T019, T020 (US3 tests) in parallel
- T023, T024 (Polish) in parallel

---

## Parallel Example: User Story 1

```bash
Task: "Write failing tests for DialoguePivotBuilder in Tests/EditMode/DialoguePivotBuilderTests.cs"
Task: "Write failing tests for DialogueAssetGenerator in Tests/EditMode/DialogueAssetGenerationTests.cs"
Task: "Add a hand-authored interchange JSON fixture under Samples/DialogueExample/"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Setup → Foundational
2. US1: a single self-contained dialogue generates correctly, text ready for a localization build
3. **STOP and VALIDATE**: run the sample fixture through the pipeline by hand, inspect the generated `DialogueGraph`
4. Already a usable, demoable capability even before sub-dialogue linking exists

### Incremental Delivery

1. Setup + Foundational → shared plumbing ready
2. + US1 → single-dialogue generation correct (MVP)
3. + US2 → dialogue-to-dialogue jumps work, the feature's actual differentiator
4. + US3 → folded into the same safe, unattended-capable pipeline as quest/flow

### Notes

- Tests are mandatory (constitution IV) — confirm each fails for the right reason before its paired implementation task.
- This feature touches two packages (`graphimport`, `graphdialoguesystem`); commit the `graphdialoguesystem` change (T012/T015) as its own logical unit, per repo convention (dedicated branch already in use: `049-dialogue-import-unity-side`).
- Avoid: vague tasks, same-file conflicts across parallel tasks, skipping the red step of red-green-refactor.
