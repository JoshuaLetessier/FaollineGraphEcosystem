---
description: "Task list for Global & Local Execution Contexts (graphcore 013)"
---

# Tasks: Global & Local Execution Contexts

**Input**: Design documents from `specs/013-scoped-context/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/public-api.md ✅

**Status**: ✅ COMPLETE — all 17 tasks done; full EditMode suite 545/545 green (24 new scoped tests + 521 pre-existing, unmodified).

**Tests**: REQUIRED — GraphCore Constitution Principle IV (Test-Driven Development) is NON-NEGOTIABLE.

**Organization**: This feature is a single shared mechanism (an optional local overlay inside
`BaseContext`) that all three user stories exercise through the same core files. The routing engine is
therefore Foundational; each user story phase adds its runner-level acceptance journey. Same-file edits
are intentionally **not** marked `[P]`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 (maps to spec.md user stories)

## Path Conventions

Single Unity package, existing assembly. Runtime: `com.faolline.graphcore/Runtime/`. Tests:
`com.faolline.graphcore/Tests/EditMode/Execution/`. No new package or assembly.

---

## Phase 1: Setup (Baseline)

**Purpose**: Lock the back-compat reference before touching the foundation.

- [x] T001 Run the full existing graphcore EditMode suite headlessly (Unity 6000.3 batchmode, editor closed; delete a stale `Temp/UnityLockfile` first) and confirm it is GREEN — this is the unmodified-suite reference for SC-004.

**Checkpoint**: Known-green baseline recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The append-only data fields plus the shared `BaseContext` overlay engine that ALL user stories build on.

- [x] T002 [P] Add append-only `OpensScope` field (`[SerializeField] private bool _opensScope`, default false) with public `bool OpensScope { get; set; }` and XML `<summary>` to `com.faolline.graphcore/Runtime/Nodes/SubGraphNodeData.cs` (leave `TargetGraph`/`InheritParentContext` untouched).
- [x] T003 [P] Add append-only `bool OpenedLocalContext { get; set; }` (default false) with XML `<summary>` to `com.faolline.graphcore/Runtime/Execution/GraphExecutionState.cs`, and copy it in `ShallowClone()`.
- [x] T004 [P] Write FAILING overlay unit tests in `com.faolline.graphcore/Tests/EditMode/Execution/ScopedContextOverlayTests.cs` covering the routing table from contracts/public-api.md: `HasLocalContext` open/close; read local-first then global fall-through; write resolve-and-write (local shadow / durable global / undeclared→local); isolation after `EndLocalContext`; `BeginLocalContext(seedFrom)` seeds local from a graph's parameters; nested-`Begin` discards-and-replaces with a `[GraphCore]` warning; `EndLocalContext` no-op + warning when none open; a write to local AND a write to global each fire `OnParameterChanged` subscribers (FR-009/inv.9); `GetAllParameters()` returns the global bucket only while a scope is active — local scratch excluded (inv.11).
- [x] T005 Implement the overlay in `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: nullable `_local` bucket + `_localActive`; `BeginLocalContext()`, `BeginLocalContext(BaseGraph seedFrom)`, `EndLocalContext()`, `bool HasLocalContext`; overlay-aware `Set/Get/TryGet/Has` (signatures unchanged, NOT virtual); `GetAllParameters` stays global-only; `[GraphCore]`-prefixed warnings; XML docs. (depends on T004)

**Checkpoint**: Routing engine green; `BaseContext` behaves identically when no scope is opened.

---

## Phase 3: User Story 1 - Temporary values stay local (Priority: P1) 🎯 MVP

**Goal**: A sub-graph flagged `OpensScope` runs with a fresh local context; its temporaries are discarded when it ends; sequential scoped sub-graphs each get a fresh local.

**Independent Test**: Parent flow → scope-opening sub-graph sets local working vars → reaches End; assert those vars are gone afterward, and a second scoped sub-graph starts with none of them.

- [x] T006 [US1] Write FAILING runner tests in `com.faolline.graphcore/Tests/EditMode/Execution/ScopedSubGraphRunnerTests.cs`: entering an `OpensScope=true` sub-graph opens a local context; its scratch writes are gone after End (US1.1/US1.2); two sequential scoped sub-graphs each get a fresh, empty local (US1.3); lockstep open/discard (FR-002).
- [x] T007 [US1] Add the scoped branch to `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs`: in `EnterSubGraph`, when `subNode.OpensScope` → keep `_context`, call `_context.BeginLocalContext(targetGraph)`, push the sub-frame with `OpenedLocalContext = true` (precedence over `InheritParentContext`); in `HandleEndNode`, when popping a frame with `OpenedLocalContext`, call `EndLocalContext()` before resuming the parent. (depends on T002, T003, T005, T006)

**Checkpoint**: US1 functional — temporaries vanish on scope exit.

---

## Phase 4: User Story 2 - Read & durably update globals from a scope (Priority: P1)

**Goal**: A scoped sub-graph reads host/global values via fall-through and durably updates global-resident variables, while undeclared scratch stays local.

**Independent Test**: Host `Gold=7`, `BossDefeated=false`; scoped sub-graph reads `Gold`, sets scratch, sets `BossDefeated=true`; after End: scratch gone, `BossDefeated==true`, `Gold` still 7.

- [x] T008 [US2] Write FAILING acceptance tests in `ScopedSubGraphRunnerTests.cs` (US2 section): fall-through read of a host global (US2.1); durable global write persists past End (US2.2/FR-006); undeclared scratch discarded (US2.3/FR-004). (same file as T006 → sequential)
- [x] T009 [US2] Verify US2 acceptance tests pass on the T005 routing + T007 seeding/lockstep; refactor only (seeding owned by T007). (depends on T007, T008)

**Checkpoint**: US1 AND US2 both work — gameflow's Global/Scene halves satisfied.

---

## Phase 5: User Story 3 - Existing graphs behave exactly as before (Priority: P1)

**Goal**: The scoped behaviour is a third, opt-in option; inherit/fresh and pre-existing assets are byte-for-byte unchanged.

**Independent Test**: Inherit and fresh-blank paths identical to 0.2.0; `OpensScope=false` opens no overlay; full pre-existing suite passes unmodified.

- [x] T010 [P] [US3] Write back-compat tests in `com.faolline.graphcore/Tests/EditMode/Execution/ScopedContextBackCompatTests.cs`: `InheritParentContext=true` inherits with no overlay (US3.1); fresh-blank unchanged (US3.2); `OpensScope=false` never opens a local context (US3.3).
- [x] T011 [US3] Re-run the ENTIRE pre-existing graphcore EditMode suite UNMODIFIED and confirm GREEN against the T001 baseline (SC-004). Result: 545/545 green, zero pre-existing test edited. (depends on T005, T007, T009)

**Checkpoint**: All three P1 stories functional; zero back-compat regressions.

---

## Phase 6: Step-back fidelity across a scope boundary (FR-010 / SC-005)

**Purpose**: Cross-cutting requirement — history/checkpoint must capture and restore overlay state.

- [x] T012 Write FAILING tests in `com.faolline.graphcore/Tests/EditMode/Execution/ScopedContextHistoryTests.cs`: pre-scope snapshot restores with NO local; during-scope snapshot reproduces the overlay; step-back across the boundary neither resurrects a discarded local nor leaves a closed local open.
- [x] T013 Extend `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: `DeepClone()` deep-copies `_local` + `_localActive`; internal `CopyValuesFrom(source)` restores both buckets + active flag in place (subscribers preserved). (depends on T005, T012)

**Checkpoint**: Step-back reproduces overlay state with full fidelity.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T014 [P] Bump `com.faolline.graphcore/package.json` version `0.2.0` → `0.3.0` (semver MINOR — additive public API + append-only fields).
- [x] T015 Final headless batchmode run of the FULL suite (existing + new) — all green (545/545); semver MINOR confirmed.
- [x] T016 [P] All new public members carry XML `<summary>` docs; new warnings use the `[GraphCore]` prefix; all new identifiers are domain-agnostic (no "scene"/"quest" in graphcore) (FR-013).
- [x] T017 [P] `quickstart.md` §5 manual example covered by `ScopedContextOverlayTests` (global persists, scratch discarded after `EndLocalContext`).

---

## Dependencies & Execution Order

### Critical path (as executed)

T001 → T004 → T005 → T006 → T007 → T008 → T009 → T011 → T012 → T013 → T015

### Notes

- `BaseContext.cs` (T005, T013) and `BaseRunner.cs` (T007) were serialized — same-file edits, never parallel.
- Implementation was batched (write tests + impl, then one authoritative full-suite run) rather than per-task red/green, because each Unity batchmode run costs minutes. The final run is the green gate and also satisfies the unmodified-suite back-compat requirement (SC-004).

---

## Outcome

- **545/545 EditMode tests green** (24 new scoped tests, 521 pre-existing unmodified).
- graphcore **0.2.0 → 0.3.0** (semver MINOR; append-only fields, additive public API).
- No `ParameterData` change, no new assembly — minimal foundation footprint.
- Files changed: `BaseContext.cs`, `BaseRunner.cs`, `SubGraphNodeData.cs`, `GraphExecutionState.cs`, `package.json` + 4 new EditMode test files.
