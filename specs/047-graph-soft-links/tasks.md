---

description: "Task list for Break Hard Graph-to-Graph Asset References (Soft Graph Links)"
---

# Tasks: Break Hard Graph-to-Graph Asset References (Soft Graph Links)

**Input**: Design documents from `/specs/047-graph-soft-links/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included and REQUIRED — Constitution IV (Test-Driven Development) is NON-NEGOTIABLE for
`com.faolline.graphcore` and is followed as ecosystem convention in every other package here. Every
implementation task has a preceding test task; tests MUST be run (Coplay MCP `run_tests`) and
confirmed failing before the matching implementation task starts.

**Organization**: Tasks are grouped by user story (spec.md priorities). US1 and US2 are both P1 and
independent of each other; US3 (P2) depends on the Foundational extension seam; US4 (P3) depends on
US1 (soft reference) and US2 (catalog) being in place, plus Addressables being installed to verify.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 (GraphLink soft reference), US2 (IGraphCatalog port), US3 (Graph Key Registry), US4 (Addressables preload)

## Path Conventions

All paths are relative to the repository root (`Assets/FaollineGraphEcosystem/`), matching the
existing per-package `Runtime/` `Editor/` `Tests/EditMode/` `Tests/PlayMode/` asmdef layout
verified in `plan.md`'s Project Structure section. No new package or assembly is created.

---

## Phase 1: Setup

**Purpose**: Confirm the branch/environment is ready; no new tooling needed (all 3 packages and
their asmdefs already exist).

- [ ] T001 Confirm `047-graph-soft-links` branch is checked out and `git status` is clean before any edit (per Development Standards: one logical unit of work per commit, no mixed changes)
- [ ] T002 Run the full existing EditMode suite once (Coplay MCP `run_tests`, all packages) to record the pre-change green baseline — this is the regression baseline SC-005 is checked against

**Checkpoint**: Baseline green suite recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The generic `graphcore` extension seam (research.md R9) that US3's concrete rule
depends on. This is NOT part of US1's own scope (US1 needs none of this — its two changes are
self-contained), but it must exist before US3 can register into it, so it is foundational rather
than folded into either story.

**⚠️ CRITICAL**: US3's validator-extension task (T021) cannot start until this phase is complete. US1, US2 do not depend on this phase and may proceed in parallel with it.

- [ ] T003 [P] Write failing EditMode test for `GraphValidatorExtensionRegistry` (register/unregister/empty-by-default) in `com.faolline.graphcore/Tests/EditMode/GraphValidatorExtensionRegistryTests.cs`
- [ ] T004 Implement `IGraphValidatorExtension` + `GraphValidatorExtensionRegistry` in `com.faolline.graphcore/Editor/Tools/GraphValidatorExtensionRegistry.cs` per `contracts/graphlink-soft-reference.md` — confirm T003 now passes
- [ ] T005 Write failing EditMode test: `GraphValidator.Validate` reports a `Warning` for a `SubGraphNodeData` when a registered `IGraphValidatorExtension.CheckSubGraphTarget` returns non-empty, and reports nothing when zero extensions are registered, in `com.faolline.graphcore/Tests/EditMode/GraphValidatorSubGraphExtensionTests.cs`
- [ ] T006 Implement the generic SubGraph-extension rule in `com.faolline.graphcore/Editor/Tools/GraphValidator.cs` (iterate `GraphValidatorExtensionRegistry.Extensions` per `SubGraphNodeData` with a resolved `TargetGraph`) — confirm T005 now passes

**Checkpoint**: Foundation ready — graphcore's generic seam exists and is empty-by-default-safe; US1, US2, US3 (validator half) can now all proceed.

---

## Phase 3: User Story 1 - Annotate a graph without paying for it (Priority: P1) 🎯 MVP candidate

**Goal**: `GraphLinkNodeData`'s target no longer appears in its owner graph's build/bundle dependency closure, with zero change to authoring ergonomics or to any other file.

**Independent Test**: Per `quickstart.md` §1-2 — create two graphs linked via `GraphLinkNodeData`, confirm `AssetDatabase.GetDependencies` no longer includes the target, confirm a real Addressables Analyze pass shows the same, confirm drag-and-drop/navigation/validator-warning all still work.

### Tests for User Story 1

- [ ] T007 [P] [US1] Write failing EditMode test: `GraphLinkNodeData.TargetGraphGuid` round-trips through `TargetGraph`'s getter/setter (assign a `BaseGraph`, confirm GUID stored; clear it, confirm null) in `com.faolline.graphcore/Tests/EditMode/GraphLinkSoftReferenceTests.cs`
- [ ] T008 [P] [US1] Write failing EditMode test: `AssetDatabase.GetDependencies(ownerGraphPath, recursive: true)` does NOT include a `GraphLinkNodeData` target's asset path (create both as temporary test assets, add/remove in teardown), in the same file as T007
- [ ] T009 [P] [US1] Write failing EditMode test: a `GraphLinkNodeData` with a non-empty `TargetGraphGuid` that resolves to no asset (bogus GUID) produces a `GraphValidator` `Warning`; a never-assigned (`empty GUID`) link produces no such warning, in `com.faolline.graphcore/Tests/EditMode/GraphValidatorSoftLinkTests.cs`

### Implementation for User Story 1

- [ ] T010 [US1] Replace `_targetGraph: BaseGraph` with `_targetGraphGuid: string` in `com.faolline.graphcore/Runtime/Nodes/GraphLinkNodeData.cs`; add `TargetGraphGuid` (plain, unguarded) and re-implement `TargetGraph` (`#if UNITY_EDITOR`, GUID↔asset via `AssetDatabase`) preserving its exact public signature per `contracts/graphlink-soft-reference.md` and `research.md` R1/R2 — confirm T007 now passes
- [ ] T011 [US1] Implement the unresolved-GraphLink-target rule in `com.faolline.graphcore/Editor/Tools/GraphValidator.cs` (depends on T010) — confirm T009 now passes
- [ ] T012 [US1] Manually verify (per `quickstart.md` §1, steps 7) that `GraphLinkNodeView`'s double-click navigation and `BaseNodeInspectorView.AddGraphLinkSection`'s `ObjectField` drag-and-drop still work unmodified against the new `TargetGraph` — no code change expected in either file (confirms `research.md` R1); if either breaks, that is a signal the property contract was not preserved and T010 needs revisiting
- [ ] T013 [US1] Real build verification (spec SC-001, cannot be an EditMode assertion): put a `GraphLinkNodeData` owner graph in an Addressables group with its target elsewhere, run Addressables ▸ Analyze (or a Player build report), confirm the target's content is absent from the owner's group — record the result in `quickstart.md`'s §1 step 6 as a completed verification, not just a code-level check
- [ ] T014 [US1] Bump `com.faolline.graphcore/package.json` version to `0.41.0` per `research.md` R10 and update its CHANGELOG entry (new public member + 2 new validator rules, no public API removed)
- [ ] T015 [US1] Run full EditMode suite (Coplay MCP `run_tests`) — confirm zero regressions in existing `GraphLinkNodeViewTests.cs`, `GraphLinkInspectorTests.cs`, `GraphLinkRunnerPassThroughTests.cs` (spec SC-005)

**Checkpoint**: User Story 1 fully functional and independently testable/shippable — this alone already delivers the "gain net sans contrepartie" lot discussed with the requester.

---

## Phase 4: User Story 2 - Resolve a graph identifier to an asset, independent of loading technology (Priority: P1)

**Goal**: A project with multiple root graphs can resolve `GraphId → BaseGraph` through a swappable seam that works identically with or without any asynchronous asset-loading technology.

**Independent Test**: Per `quickstart.md` §3 — register two graphs in a `DirectGraphCatalog` (zero Addressables), capture+restore a `GraphRunSnapshot` by identifier alone, confirm an unregistered id fails cleanly.

### Tests for User Story 2

- [ ] T016 [P] [US2] Write failing EditMode test: `DirectGraphCatalog.Resolve` invokes `onResolved` exactly once for a registered `graphId`, and `onFailed` exactly once (never `onResolved` with null) for an unregistered one, in `com.faolline.graphgameflow/Tests/EditMode/DirectGraphCatalogTests.cs`
- [ ] T017 [P] [US2] Write failing EditMode test: `GameFlowContext.GraphCatalog` defaults to `null`, is settable, and survives `DeepClone` as a shared reference (mirroring the existing `SceneLoader` clone test pattern) in `com.faolline.graphgameflow/Tests/EditMode/GameFlowContextGraphCatalogTests.cs`
- [ ] T018 [P] [US2] Write failing EditMode test: end-to-end save/restore — register 2 graphs in a `DirectGraphCatalog`, capture a `GraphRunSnapshot` from one, resolve via the catalog by `GraphId` alone, call `Restore`, confirm the run resumes on the correct graph with no caller-side lookup table, in `com.faolline.graphgameflow/Tests/EditMode/GraphCatalogSaveRestoreTests.cs`

### Implementation for User Story 2

- [ ] T019 [P] [US2] Implement `IGraphCatalog` in `com.faolline.graphgameflow/Runtime/Graph/IGraphCatalog.cs` per `contracts/graph-catalog-port.md` (callback-based, not `Task` — research.md R4)
- [ ] T020 [US2] Implement `DirectGraphCatalog` in `com.faolline.graphgameflow/Runtime/Graph/DirectGraphCatalog.cs` (depends on T019) — confirm T016 now passes
- [ ] T021 [US2] Add `GraphCatalog` property to `com.faolline.graphgameflow/Runtime/Context/GameFlowContext.cs`, mirroring the existing `SceneLoader` property and its `DeepClone` treatment exactly — confirm T017 now passes
- [ ] T022 [US2] Confirm T018 now passes against T019-T021 with zero `com.faolline.graphsave` code changes (spec Assumptions — this lot is consumed by `graphsave`, not modified by it)
- [ ] T023 [US2] Bump `com.faolline.graphgameflow/package.json` version to `0.17.0` (partial — US3 also lands in this same package/version; do not bump twice) and add a CHANGELOG entry for the `IGraphCatalog`/`DirectGraphCatalog`/`GameFlowContext.GraphCatalog` addition
- [ ] T024 [US2] Run full EditMode suite for `com.faolline.graphgameflow` — confirm zero regressions (spec SC-005/SC-006)

**Checkpoint**: User Story 2 fully functional and independently testable — `graphsave` now has everything it needs for multi-root-graph restore, with or without Addressables.

---

## Phase 5: User Story 3 - Mark a graph as a chapter entry point from an editor tool (Priority: P2)

**Goal**: An author can see known graph keys, which asset each resolves to, and promote a graph asset to a key from a dedicated editor tool — and a `SubGraphNodeData` that accidentally crosses into a promoted graph is flagged by the validator.

**Independent Test**: Per `quickstart.md` §4-5 — open the Graph Key Registry window against a project with a fake provider registered; separately confirm the validator extension fires only when a target is actually promoted.

**Depends on**: Phase 2 (Foundational seam) for T029-T030.

### Tests for User Story 3

- [ ] T025 [P] [US3] Write failing EditMode test: `GraphKeySourceRegistry.Register`/`Unregister`/`Providers` (empty by default, idempotent register, mirrors `SceneKeySourceRegistry`'s own test shape) in `com.faolline.graphgameflow/Tests/EditMode/GraphKeySourceRegistryTests.cs`
- [ ] T026 [P] [US3] Write failing EditMode test: a fake `IGraphKeySourceProvider`'s `TryResolveGuid` is reachable through `GraphKeySourceRegistry.Providers` and correctly distinguishes a promoted GUID from a non-promoted one, in the same file as T025
- [ ] T027 [P] [US3] Write failing EditMode test: `ChapterRootSubGraphValidatorExtension.CheckSubGraphTarget` returns non-empty for a graph a fake provider reports as promoted, and null for one it doesn't, in `com.faolline.graphgameflow/Tests/EditMode/ChapterRootSubGraphValidatorExtensionTests.cs`

### Implementation for User Story 3

- [ ] T028 [P] [US3] Implement `IGraphKeySourceProvider` + `GraphKeySourceRegistry` in `com.faolline.graphgameflow/Editor/Inspector/GraphKeySourceRegistry.cs`, mirroring `SceneKeySourceRegistry.cs` plus the new `TryResolveGuid` member (research.md R7) — confirm T025-T026 now pass
- [ ] T029 [US3] Implement `ChapterRootSubGraphValidatorExtension` in `com.faolline.graphgameflow/Editor/Tools/ChapterRootSubGraphValidatorExtension.cs`, self-registering into `graphcore`'s `GraphValidatorExtensionRegistry` (Phase 2) via `[InitializeOnLoadMethod]` — confirm T027 now passes
- [ ] T030 [US3] End-to-end verification per `quickstart.md` §5 step 3: a `SubGraphNodeData` targeting a graph actually promoted through `GraphKeySourceRegistry` produces a `GraphValidator` warning; one targeting a non-promoted graph does not
- [ ] T031 [US3] Implement `GraphKeyRegistryWindow` in `com.faolline.graphgameflow/Editor/Tools/GraphKeyRegistryWindow.cs` (menu `Faolline ▸ Graph ▸ Graph Key Registry`) listing project `BaseGraph` assets + `GraphId` + per-provider promotion status + "Mark as {SourceLabel}" button, per `research.md` R6/`data-model.md`
- [ ] T032 [US3] Manual verification per `quickstart.md` §4: open the window, confirm empty-by-default with no provider registered, confirm listing/promotion behavior with a test provider registered
- [ ] T033 [US3] Finalize `com.faolline.graphgameflow/package.json` version at `0.17.0` (same bump as T023 — one version covers both US2 and US3) and CHANGELOG entry for `GraphKeySourceRegistry`/`GraphKeyRegistryWindow`/`ChapterRootSubGraphValidatorExtension`
- [ ] T034 [US3] Run full EditMode suite for `com.faolline.graphgameflow` and `com.faolline.graphcore` together — confirm zero regressions

**Checkpoint**: User Stories 1, 2, AND 3 all independently functional. This is the natural "core" release point if Addressables adoption (US4) is deferred.

---

## Phase 6: User Story 4 - Preload the next chapter ahead of time (Priority: P3)

**Goal**: In a project using Addressables, an author can trigger an early asynchronous load of the next chapter's graph via a soft `AssetReferenceT`, with the current chapter's build never depending on it.

**Independent Test**: Per `quickstart.md` §6 — mark a graph Addressable, wire `PreloadNextChapterAction` early in a chapter, confirm the target is ready by chapter end and absent from the current chapter's Analyze report.

**Depends on**: US1 (soft-reference pattern proven), US2 (`IGraphCatalog` contract this adapter implements), US3 (promotion tooling used to mark the target Addressable). Requires Addressables installed to execute/verify — this package already depends on it (matches `AddressablesSceneLoader` precedent).

### Tests for User Story 4

- [ ] T035 [P] [US4] Write failing EditMode test: `AddressablesGraphCatalog.Resolve` for a valid Addressable graph key invokes `onResolved` with the correct `BaseGraph`; for an invalid key invokes `onFailed`, in `com.faolline.graphgameflow.addressables/Tests/EditMode/AddressablesGraphCatalogTests.cs`
- [ ] T036 [P] [US4] Write failing EditMode test: `AddressablesGraphKeyProvider.GetKeys()` lists only `BaseGraph`-typed Addressable entries (mirroring `AddressablesSceneKeyProviderTests.cs`'s shape, filtered by type instead of `SceneAsset`), and `TryResolveGuid` correctly identifies a promoted graph's GUID, in `com.faolline.graphgameflow.addressables/Tests/EditMode/AddressablesGraphKeyProviderTests.cs`
- [ ] T037 [P] [US4] Write failing EditMode test: `PreloadNextChapterAction.Execute` returns synchronously (no blocking), and (via a stubbed/fast Addressables load in test) the resolved graph becomes available through its configured completion path, in `com.faolline.graphgameflow.addressables/Tests/EditMode/PreloadNextChapterActionTests.cs`

### Implementation for User Story 4

- [ ] T038 [P] [US4] Implement `AddressablesGraphCatalog : IGraphCatalog` in `com.faolline.graphgameflow.addressables/Runtime/AddressablesGraphCatalog.cs`, mirroring `AddressablesSceneLoader`'s async-operation-polling shape — confirm T035 now passes
- [ ] T039 [P] [US4] Implement `AddressablesGraphKeyProvider : IGraphKeySourceProvider` in `com.faolline.graphgameflow.addressables/Editor/AddressablesGraphKeyProvider.cs`, mirroring `AddressablesSceneKeyProvider.cs` (`[InitializeOnLoadMethod]` self-registration) — confirm T036 now passes
- [ ] T040 [US4] Implement `PreloadNextChapterAction : BaseAction` in `com.faolline.graphgameflow.addressables/Runtime/PreloadNextChapterAction.cs` with `[SerializeField] AssetReferenceT<BaseGraph> _nextChapter`, supporting both usage forms (early-trigger-then-`OnEnded`-reboot; park-on-signal via existing `AwaitSignalNames`) per `data-model.md` — confirm T037 now passes
- [ ] T041 [US4] Real build verification (spec SC-001/SC-007, cannot be an EditMode assertion): mark a chapter's root graph Addressable via T031's window, wire `PreloadNextChapterAction` in an earlier chapter, run Addressables ▸ Analyze on the earlier chapter's group, confirm the marked chapter's content is absent — record as a completed `quickstart.md` §6 verification
- [ ] T042 [US4] Manual verification per `quickstart.md` §6 step 3: run the chapter to completion, confirm the preloaded graph is available with no additional wait when the driver reboots onto it (both usage forms from spec User Story 4)
- [ ] T043 [US4] Bump `com.faolline.graphgameflow.addressables/package.json` version to `0.5.0` and CHANGELOG entry
- [ ] T044 [US4] Run full EditMode suite for `com.faolline.graphgameflow.addressables` — confirm zero regressions against existing `AddressablesSceneLoaderTests.cs`/`AddressablesSceneKeyProviderTests.cs`

**Checkpoint**: All four user stories independently functional. Full feature complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T045 [P] Confirm `com.faolline.graphcore`'s asmdef references show zero addition of `com.unity.addressables`, direct or transitive (spec FR-012/Constitution II — the one absolute constraint every lot must not touch)
- [ ] T046 [P] Update each touched package's `README.md` (graphcore, graphgameflow, graphgameflow.addressables) documenting the new soft-reference behavior and new seams, per Constitution "Documentation" standard
- [ ] T047 Run the complete `quickstart.md` walkthrough end-to-end (all 6 sections) in a single pass as the final acceptance gate
- [ ] T048 Run the full ecosystem EditMode suite one final time (Coplay MCP `run_tests`) across all touched + consuming packages (`graphcore`, `graphgameflow`, `graphgameflow.addressables`, and a smoke pass against `graphsave` since it consumes US2) — confirm 100% green before merge
- [ ] T049 Semver gate check (Constitution Review & Quality Gates): confirm the 3 version bumps (T014, T023/T033, T043) and their CHANGELOG rationales are present and correct before PR

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS only US3's validator-extension tasks (T029), not US1/US2
- **US1 (Phase 3)**: Depends on Setup only — fully independent of Phase 2, US2, US3, US4
- **US2 (Phase 4)**: Depends on Setup only — fully independent of Phase 2, US1, US3, US4
- **US3 (Phase 5)**: Depends on Setup AND Foundational (Phase 2, for T029/T030) — independent of US1/US2's own code, though its validator check exercises `SubGraphNodeData`/`GraphValidator` from Lot 1's package
- **US4 (Phase 6)**: Depends on US1 (soft-ref pattern), US2 (`IGraphCatalog` contract it implements), and US3 (promotion tooling it uses in verification) — the one genuinely sequential story
- **Polish (Phase 7)**: Depends on all four user stories being complete

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories — can ship alone as the "gain net sans contrepartie" MVP
- **US2 (P1)**: No dependencies on other stories — can ship alone or alongside US1
- **US3 (P2)**: Depends on Foundational phase; otherwise independent of US1/US2 code (though conceptually extends what US1's validator started)
- **US4 (P3)**: Depends on US1 + US2 + US3 all being present

### Parallel Opportunities

- T003 [P] and all of Phase 3/Phase 4's test-writing tasks (T007-T009, T016-T018) can run in parallel with each other and with Phase 2, since US1/US2 don't depend on Phase 2
- Within US1: T007, T008, T009 in parallel (different assertions, same/adjacent files but no shared mutable state)
- Within US2: T016, T017, T018 in parallel; T019 in parallel with nothing (foundation for T020)
- Within US3: T025, T026, T027 in parallel; T028 and T039 (different packages) in parallel
- Within US4: T035, T036, T037 in parallel; T038, T039 in parallel (different files)
- Phase 7's T045, T046 in parallel

---

## Parallel Example: User Story 1

```
Task: "Write failing EditMode test: TargetGraphGuid round-trip in GraphLinkSoftReferenceTests.cs" (T007)
Task: "Write failing EditMode test: AssetDatabase.GetDependencies excludes the target" (T008)
Task: "Write failing EditMode test: GraphValidator warns on unresolved GUID target" (T009)
```

All three touch the same test file or its sibling but assert independent behavior with no shared
mutable fixture state — safe to author in parallel, then implement T010-T011 once all three are
confirmed red.

---

## Implementation Strategy

### MVP First

US1 alone is a complete, shippable MVP — it is the lot the requester explicitly called "gain net
sans contrepartie" regardless of what happens to the other three lots. Recommended order:

1. Phase 1 (Setup)
2. Phase 3 (US1) — **STOP, ship if desired**
3. Phase 4 (US2) — required by `graphsave` independent of Addressables; **STOP, ship if desired**
4. Phase 2 (Foundational) + Phase 5 (US3)
5. Phase 6 (US4) — only if/when Addressables adoption is actually happening

### Incremental Delivery

Each checkpoint above (end of Phase 3, 4, 5, 6) is an independently valid release point per
package version (graphcore 0.41.0 lands with US1; graphgameflow 0.17.0 lands with US2+US3 combined
since they share a package; graphgameflow.addressables 0.5.0 lands with US4 alone).
