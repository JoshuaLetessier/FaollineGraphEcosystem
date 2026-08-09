---
description: "Task list for feature implementation"
---

# Tasks: Quest & Flow Graph Generation from Structured Data

**Input**: Design documents from `/specs/048-quest-data-import/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/runtime-api.md, quickstart.md

**Tests**: Included and REQUIRED, not optional — constitution principle IV (Test-Driven Development, NON-NEGOTIABLE) mandates a failing test before every piece of new behavior. Each implementation task explicitly depends on its preceding test task.

**Organization**: Tasks are grouped by user story (spec.md priorities) so each can be implemented, tested, and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Maps the task to US1–US4 from spec.md
- All file paths are relative to `com.faolline.graphimport/` unless noted otherwise

---

## Phase 1: Setup

**Purpose**: Package scaffolding — nothing story-specific yet

- [ ] T001 Create the package skeleton: `package.json`, `Runtime/com.faolline.graphimport.Runtime.asmdef`, `Editor/com.faolline.graphimport.Editor.asmdef`, `Tests/EditMode/com.faolline.graphimport.Tests.EditMode.asmdef`, and the empty subfolders (`Runtime/Mapping`, `Runtime/Resolution`, `Runtime/Sources`, `Runtime/Pivot`, `Runtime/Branching`, `Runtime/Planning`, `Editor/Apply`, `Editor/Generation`, `Editor/Window`, `Samples/CryptiqueExample`) per `plan.md` Project Structure
- [ ] T002 Add `com.unity.nuget.newtonsoft-json` to `Packages/manifest.json` (project) and as a `package.json` dependency of `com.faolline.graphimport`, per `research.md` §6
- [ ] T003 [P] Declare `com.faolline.graphquest`, `com.faolline.graphgameflow`, and `com.faolline.graphcore` as `package.json` dependencies of `com.faolline.graphimport`, pinned to their current floor versions; add matching asmdef references on `Editor.asmdef` (`graphquest`, `graphgameflow`) and `Runtime.asmdef` (`graphcore` only — `graphquest`/`graphgameflow` builders are consumed from `Editor`, per the Runtime/Editor split in `plan.md`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Raw table ingestion — every user story depends on being able to read a source table into memory

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 [P] Implement `SourceTable`/`SourceRow` in `Runtime/Sources/SourceTable.cs` per `data-model.md` Input stage
- [ ] T005 [P] Write failing EditMode tests for `CsvRowSource` in `Tests/EditMode/CsvRowSourceTests.cs`: plain rows, quoted fields containing commas, quoted fields containing embedded newlines, header-to-column mapping (RFC 4180 per `research.md` §2)
- [ ] T006 Implement `IRowSource` + `CsvRowSource` in `Runtime/Sources/CsvRowSource.cs` to make T005 pass (depends on: T004, T005)
- [ ] T007 [P] Write failing EditMode tests for `JsonRowSource` in `Tests/EditMode/JsonRowSourceTests.cs`: array-of-objects JSON → `SourceTable`, non-string field values coerced to string
- [ ] T008 Implement `JsonRowSource` in `Runtime/Sources/JsonRowSource.cs` to make T007 pass (depends on: T004, T007)

**Checkpoint**: Both source formats parse into `SourceTable`/`SourceRow` — user story work can begin

---

## Phase 3: User Story 1 - Map an existing production spreadsheet without reshaping it (Priority: P1) 🎯 MVP part 1

**Goal**: Declare a mapping over real multi-table data (mapped + ignored columns, ID-or-name references) and get back correct pivot fields with zero effect from unmapped data.

**Independent Test**: Feed a multi-table sample (mapped + irrelevant columns, mixed ID/name references) through `MappingConfig` + `PivotBuilder` and verify the pivot contains exactly the mapped fields, correctly cross-referenced, with unmapped columns having no effect.

### Tests for User Story 1 ⚠️ write first, confirm they fail

- [ ] T009 [P] [US1] Write failing tests for `MappingConfig.LoadFromJson`/`Validate` in `Tests/EditMode/MappingConfigTests.cs`: unmapped columns are inert (FR-014), a mapped column missing from the actual source header raises an early, specific error (Edge Cases)
- [ ] T010 [P] [US1] Write failing tests for `IdOrNameReferenceResolver` in `Tests/EditMode/IdOrNameReferenceResolverTests.cs`: resolves via stable ID, resolves via fallback name column, raises `ReferenceResolutionException` (naming source table/row/column/value) on zero matches and on ambiguous matches (FR-002, FR-003) — model the fixture on the real Cryptique data where `Puzzles."Quête liée"` references by Name while `Sequence."Quête (ID)"` references by ID

### Implementation for User Story 1

- [ ] T011 [US1] Implement `MappingConfig`/`TableMapping`/`FieldMapping`/`ReferenceMapping` (+ `LoadFromJson`, `Validate`) in `Runtime/Mapping/MappingConfig.cs` (depends on: T009)
- [ ] T012 [US1] Implement `ReferenceIndex`, `IdOrNameReferenceResolver`, `ReferenceResolutionException` in `Runtime/Resolution/` (depends on: T004, T010)
- [ ] T013 [US1] Implement a minimal `PivotBuilder` (`Runtime/Pivot/PivotBuilder.cs`) producing `PivotQuest.Fields`/`Name`/`Id` and resolving non-step `PivotReference`s (triggers/triggeredBy) — step/branch construction deferred to US4 (depends on: T011, T012)
- [ ] T014 [P] [US1] Add the sanitized Cryptique-derived sample tables (Quêtes, Puzzles at minimum) and a matching mapping config under `Samples/CryptiqueExample/` as the shared fixture for T010, T013, and later stories (strip Discord IDs, Notes, Statut, script-location columns per the earlier "generic, not project-locked" decision)

**Checkpoint**: US1 independently testable and demoable — real multi-table data in, correctly mapped/resolved pivot fields out, bad references fail loud.

---

## Phase 4: User Story 2 - Preview and control where generated assets land (Priority: P1) 🎯 MVP part 2

**Goal**: Turn pivot data into a full, editable preview of what would be generated, with nothing written to disk until committed.

**Independent Test**: Run the plan builder against US1's pivot output and verify a complete, accurate `GenerationPlan` is produced with zero disk writes; verify editing one entry's `ProposedPath` before commit changes only that entry.

### Tests for User Story 2 ⚠️ write first, confirm they fail

- [ ] T015 [P] [US2] Write failing tests for `TemplatePathResolver` in `Tests/EditMode/TemplatePathResolverTests.cs`: per-`PlanEntryKind` template substitution from `PivotQuest` fields (e.g. `{chapter}`, `{name}`)
- [ ] T016 [P] [US2] Write failing tests for `PlanBuilder` in `Tests/EditMode/PlanBuilderTests.cs`: one entry per pivot quest per asset kind, no disk access (FR-009), identical input → identical plan across two runs (SC-003)

### Implementation for User Story 2

- [ ] T017 [US2] Implement `IPathTemplateResolver` + `TemplatePathResolver` in `Runtime/Planning/TemplatePathResolver.cs` (depends on: T015)
- [ ] T018 [US2] Implement `GenerationPlan`/`PlanEntry` + `PlanBuilder` in `Runtime/Planning/PlanBuilder.cs` (depends on: T013, T016, T017)
- [ ] T019 [US2] Implement the Editor review window skeleton in `Editor/Window/GraphImportWindow.cs`: load mapping + tables, run `PivotBuilder` + `PlanBuilder`, list `plan.Entries` with an editable `ProposedPath` field per row (depends on: T018)

**Checkpoint**: US2 independently testable and demoable — a plan can be produced headlessly and reviewed/edited in the Editor window; MVP (US1+US2) delivers "map real data → see exactly what would be generated, adjustable" end to end, still without ever writing an asset.

---

## Phase 5: User Story 3 - Regenerate safely from an automated pipeline (Priority: P2)

**Goal**: Applying a plan never silently overwrites or silently skips a colliding asset — every collision is visible to both CI and a human.

**Independent Test**: Run generation twice against data that collides on the second run; verify the second run neither overwrites nor silently omits the colliding asset, and produces a report identifying it.

### Tests for User Story 3 ⚠️ write first, confirm they fail

- [ ] T020 [P] [US3] Write failing tests for `PlanConflictDetector` in `Tests/EditMode/PlanConflictDetectorTests.cs` (against a scratch `AssetDatabase` folder): an existing asset at a proposed path → conflict; two plan entries proposing the identical path → conflict; a fully clean plan → `IsClean == true`
- [ ] T021 [P] [US3] Write failing tests for `PlanApplier` in `Tests/EditMode/PlanApplierTests.cs`: creates only entries absent from the conflict report, never overwrites an existing asset, returns exactly the entries it created

### Implementation for User Story 3

- [ ] T022 [US3] Implement `ConflictReport`/`ConflictEntry` + `PlanConflictDetector` in `Editor/Apply/PlanConflictDetector.cs` (depends on: T018, T020)
- [ ] T023 [US3] Implement `PlanApplier` in `Editor/Apply/PlanApplier.cs` (depends on: T022, T021)
- [ ] T024 [US3] Implement `GraphImportPipeline.Run` (mapping + tables → pivot → plan → detect → apply → report) as the single unattended/CI entry point in `Editor/GraphImportPipeline.cs` (depends on: T013, T018, T023)
- [ ] T025 [US3] Wire the review window (T019) to display `ConflictReport.Conflicts` before commit and block committing conflicting entries, reusing the same `ConflictReport` the CI path consumes (FR-013) (depends on: T019, T022)

**Checkpoint**: US3 independently testable and demoable — re-running against colliding data is provably safe (no overwrite, no silent skip) in both the headless and interactive path, from one shared report.

---

## Phase 6: User Story 4 - Get a playable branching flow, not just a flat quest list (Priority: P2)

**Goal**: Step-sequence data with a declared outcome column produces real branches in a playable flow graph, with step content referenced (not inlined) via `SubGraphNodeData`.

**Independent Test**: Feed step-sequence data with two same-position steps under one quest, each with a distinct declared outcome, and verify the generated flow has two branches gated on those outcomes, each referencing its content rather than embedding it.

### Tests for User Story 4 ⚠️ write first, confirm they fail

- [ ] T026 [P] [US4] Write failing tests for `DeclaredColumnBranchStrategy` in `Tests/EditMode/DeclaredColumnBranchStrategyTests.cs`: distinct declared outcomes at a shared position → distinct branches; missing or duplicate outcome at a shared position → `BranchDetectionException`, never an inferred/guessed order (FR-005, FR-006) — use the Q_001 "Victoire/Défaite contre le joueur de dé" fixture (extended with a declared outcome column) from the real dataset
- [ ] T027 [P] [US4] Write failing tests for `PivotBuilder`'s step/branch construction in `Tests/EditMode/PivotBuilderStepTests.cs`: steps ordered correctly, `PivotStep.ContentRef` resolves to the puzzle/dialogue row without inlining its data (FR-008)

### Implementation for User Story 4

- [ ] T028 [US4] Implement `IBranchDetectionStrategy` + `DeclaredColumnBranchStrategy` + `BranchDetectionException` in `Runtime/Branching/DeclaredColumnBranchStrategy.cs` (depends on: T026)
- [ ] T029 [US4] Extend `PivotBuilder` to build `PivotStep`/`PivotBranch` per quest via the branch strategy (depends on: T013, T027, T028)
- [ ] T030 [US4] Implement `IQuestAssetGenerator` using `graphquest`'s fluent builder in `Editor/Generation/QuestAssetGenerator.cs` (depends on: T018)
- [ ] T031 [US4] Implement `IFlowAssetGenerator` using `graphgameflow` primitives, wiring each step's `ContentRef` through `SubGraphNodeData` per constitution principle VII (depends on: T029, T030)
- [ ] T032 [US4] Extend `PlanBuilder` to emit both a `QuestAsset` and a `FlowAsset` `PlanEntry` per pivot quest (depends on: T018, T029)
- [ ] T033 [US4] Wire `PlanApplier` to dispatch each `PlanEntry.Kind` to `QuestAssetGenerator`/`FlowAssetGenerator` (depends on: T023, T030, T031)

**Checkpoint**: All four user stories independently functional — a declared branch produces a real branching `graphgameflow` asset referencing puzzle/dialogue subgraphs, alongside its `graphquest` asset, through the same safe plan/apply pipeline.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T034 [P] Extend `Samples/CryptiqueExample/` to a full end-to-end fixture (Sequence + Dialogues added, with a declared outcome column) and validate `quickstart.md` against it headlessly
- [ ] T035 [P] Add XML `<summary>` documentation to every public `Runtime`/`Editor` type, per constitution Development Standards
- [ ] T036 Run the full Coplay MCP pre-merge gate sequence: `validate_script` → `unity_reflect` → `manage_packages` → `run_tests` (full EditMode suite) → `read_console` (zero errors), per constitution Review & Quality Gates
- [ ] T037 Set `com.faolline.graphimport` `package.json` to its initial `0.1.0` release with a semver rationale note, per constitution Semver gate

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: depends on Foundational only
- **US2 (Phase 4)**: depends on Foundational + US1's `PivotBuilder` (T013) for something to plan over
- **US3 (Phase 5)**: depends on US2's `GenerationPlan`/`PlanBuilder` (T018)
- **US4 (Phase 6)**: depends on US1's `PivotBuilder` (T013, extends it) and US2/US3's plan/apply machinery (T018, T023, T030 pattern)
- **Polish (Phase 7)**: depends on all four user stories

Note: unlike a typical fully-parallel-stories template, US2→US3→US4 have a real linear dependency here because each stage of the plan/apply pipeline builds on the previous one's data shapes (`GenerationPlan` → `ConflictReport`/`PlanApplier` → per-kind generators). US1 is the only story usable in true isolation from the others.

### Parallel Opportunities

- T004, T005, T007 (Phase 2) in parallel
- T009, T010 (US1 tests) in parallel; T014 in parallel with T011–T013
- T015, T016 (US2 tests) in parallel
- T020, T021 (US3 tests) in parallel
- T026, T027 (US4 tests) in parallel
- T034, T035 (Polish) in parallel

---

## Parallel Example: User Story 1

```bash
# Tests together:
Task: "Write failing tests for MappingConfig in Tests/EditMode/MappingConfigTests.cs"
Task: "Write failing tests for IdOrNameReferenceResolver in Tests/EditMode/IdOrNameReferenceResolverTests.cs"

# Sample fixture in parallel with the mapping/resolution implementation:
Task: "Add sanitized Cryptique-derived sample tables + mapping config under Samples/CryptiqueExample/"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Setup → Foundational
2. US1: real data maps and resolves correctly
3. US2: a full, editable preview comes out the other end, nothing written yet
4. **STOP and VALIDATE**: run T014's sample through T013+T018 by hand, inspect the resulting plan
5. This is already a usable, demoable tool (inspect-only) even before US3/US4 exist

### Incremental Delivery

1. Setup + Foundational → raw ingestion works
2. + US1 → mapping/resolution correct against real data
3. + US2 → full preview, editable, MVP demoable
4. + US3 → safe to run unattended/CI, never silently destructive
5. + US4 → real branching flow assets, the actual payoff of the feature

### Notes

- Tests are mandatory here (constitution IV), not optional — every implementation task lists the test task(s) it depends on; confirm each test fails for the right reason before starting the paired implementation task.
- Commit after each task or logical group, per repo convention (dedicated branch already in use: `048-quest-data-import`).
- Avoid: vague tasks, same-file conflicts across parallel tasks, skipping the red step of red-green-refactor.
