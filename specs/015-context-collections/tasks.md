---
description: "Task list for 015-context-collections (P2 — named string-set collections on BaseContext)"
---

# Tasks: P2 — Context collections (named string-sets)

**Input**: Design documents from `specs/015-context-collections/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED — constitution mandates TDD. EditMode only (headless). Run via Unity 6000.3 batchmode
(editor CLOSED; `-runTests -testPlatform EditMode` WITHOUT `-quit`; re-run once after source changes;
verify the results XML — see memory `maximize-headless-testing`).

**Organization**: by user story. US1 (store) is the MVP; US2 (durability) and US3 (notifications) build on
it; US4 (authoring) is exercised in graphTest. Branch is `015-context-collections` (P1 signals included).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different file, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 / US4 (omitted for Setup / Back-compat / Polish)

## Path Conventions

graphcore lib root: `com.faolline.graphcore/`. Sandbox: `com.faolline.graphTest/`. Repository-relative.

---

## Phase 1: Setup

- [ ] T001 Create folders `com.faolline.graphcore/Tests/EditMode/Collections/`, `com.faolline.graphTest/Runtime/Actions/` (exists) and confirm `com.faolline.graphTest/Runtime/Conditions/` exists.

---

## Phase 2: Foundational

**Purpose**: none beyond Setup — the collection store is itself US1. (No blocking shared type like P1's SignalArgs.)

**Checkpoint**: proceed to US1.

---

## Phase 3: User Story 1 — Hold and mutate set-valued state (Priority: P1) 🎯 MVP

**Goal**: add/remove/contains/count/enumerate/clear named string-sets with set semantics and an independent keyspace.

**Independent Test**: add "a","b","a" to "items" → count 2, contains "a"; remove "a" → count 1; enumerate {"b"}; clear → 0.

### Tests (write FIRST, confirm via batchmode) ⚠️

- [ ] T002 [P] [US1] Write `CollectionStoreTests` in `com.faolline.graphcore/Tests/EditMode/Collections/CollectionStoreTests.cs`: idempotent add + count (INV-1); remove drops membership; absent collection → contains false / count 0 / GetCollection empty-non-null / remove+clear no-op (INV-2); independent keyspace vs a scalar of the same key (INV-3); GetCollection returns an independent snapshot (mutating it doesn't change state, INV-5); null/empty key & null item → `[GraphCore]` warn + no-op (INV-9). Confirm RED.

### Implementation

- [ ] T003 [US1] Implement the collection store in `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: lazy `_collections` (Dictionary<string,HashSet<string>>); `AddToCollection`/`RemoveFromCollection`/`CollectionContains`/`CollectionCount`/`GetCollection` (read-only snapshot copy)/`ClearCollection`; null/empty-key & null-item guards with `[GraphCore]` warnings. Do NOT touch `_params`/`GetAllParameters`/overlay. XML docs. Confirm T002 GREEN.

**Checkpoint**: US1 usable — set state can be accumulated and queried.

---

## Phase 4: User Story 2 — Durable across history and save (Priority: P1)

**Goal**: collections survive step-back (deep-copied) and are exposed for save in parallel to scalars.

**Independent Test**: snapshot with {"a"}, add "b", step back → {"a"}. Save snapshot lists collections; scalar snapshot lists none.

**Depends on**: US1.

### Tests (write FIRST) ⚠️

- [ ] T004 [P] [US2] Write `CollectionDurabilityTests` in `com.faolline.graphcore/Tests/EditMode/Collections/CollectionDurabilityTests.cs`: `DeepClone` produces independent copies (mutating clone ≠ source, INV-8); `GetAllCollections` snapshots all collections as read-only copies (INV-7); `GetAllParameters` excludes collections and is unchanged (INV-7); `CopyValuesFrom` (via a clone round-trip) restores collections. Confirm RED.
- [ ] T005 [P] [US2] Write `CollectionStepBackTests` in `com.faolline.graphcore/Tests/EditMode/Execution/CollectionStepBackTests.cs`: drive a `BaseRunner` over a small graph, mutate a collection across a node boundary, `GoBack`, assert the collection holds exactly the pre-snapshot membership (INV-8 end-to-end). Confirm RED.

### Implementation

- [ ] T006 [US2] In `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: add `GetAllCollections()` (IReadOnlyDictionary<string,IReadOnlyCollection<string>> of copies); extend `DeepClone()` to deep-copy `_collections` (new HashSet per key); extend internal `CopyValuesFrom()` to clear+rebuild `_collections` from the source. `GetAllParameters` untouched. XML docs. Confirm T004 + T005 GREEN.

**Checkpoint**: US1 + US2 = durable, persistable set state (the MVP for the Reactive engine).

---

## Phase 5: User Story 3 — React to collection changes (Priority: P2)

**Goal**: a real membership change fires a per-key notification; idempotent ops are silent.

**Independent Test**: subscribe to K; add new → fires; add same → silent; remove present → fires; remove absent → silent.

**Depends on**: US1.

### Tests (write FIRST) ⚠️

- [ ] T007 [P] [US3] Write `CollectionNotificationTests` in `com.faolline.graphcore/Tests/EditMode/Collections/CollectionNotificationTests.cs`: notify once on new add; silent on idempotent add; notify on present-remove; silent on absent-remove; notify on clear of a non-empty collection, silent on clear of empty; handler receives the collection key (INV-4); `OffCollectionChanged` stops delivery; re-entrant subscribe/unsubscribe during delivery is safe (INV-4). Confirm RED.

### Implementation

- [ ] T008 [US3] In `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: add lazy `_collectionSubs`; `OnCollectionChanged`/`OffCollectionChanged` (Action<string>, null/empty-key guarded); fire from Add/Remove/Clear ONLY on real membership change, iterating a subscriber snapshot. XML docs. Confirm T007 GREEN.

**Checkpoint**: all three context stories functional.

---

## Phase 6: Back-compat + overlay-independence (the non-breakage gate)

- [ ] T009 [P] Write `CollectionBackCompatTests` in `com.faolline.graphcore/Tests/EditMode/Execution/CollectionBackCompatTests.cs`: collection ops target the global store while a local context is open and survive `EndLocalContext` (INV-6); opening/ending a local context never branches/discards collections; a context using no collections is identical to 0.4.0 (INV-10). Confirm RED then GREEN.
- [ ] T010 Run the ENTIRE pre-existing graphcore + graphTest EditMode suite (560) UNMODIFIED via batchmode and confirm 100% green — the non-breakage gate (SC-002). Record the pass count.

---

## Phase 7: User Story 4 — Author with collections (Priority: P2 / FR-013)

**Goal**: membership & count-threshold conditions and a recipe action, exercised in graphTest.

**Independent Test**: edge gated by "K contains X" taken iff present; recipe over {"x","y"}→"z" consumes and produces.

**Depends on**: US1 (store), US3 not required.

### Tests (write FIRST) ⚠️

- [ ] T011 [P] [US4] Write `CollectionExerciseTests` in `com.faolline.graphTest/Tests/EditMode/Runtime/CollectionExerciseTests.cs`: a membership-gated edge selected only when the set contains the item; a count-threshold-gated edge passing at N; a recipe consuming {"x","y"} to produce "z" (and making no change when a required element is missing). Confirm RED.

### Implementation

- [ ] T012 [P] [US4] Create `com.faolline.graphTest/Runtime/Conditions/TestCollectionContainsCondition.cs` (Key, Item, optional Negate; reads `CollectionContains`), XML docs, `[CreateAssetMenu]`.
- [ ] T013 [P] [US4] Create `com.faolline.graphTest/Runtime/Conditions/TestCollectionCountCondition.cs` (Key, ComparisonOperator, Value; compares `CollectionCount`), XML docs, `[CreateAssetMenu]`.
- [ ] T014 [P] [US4] Create `com.faolline.graphTest/Runtime/Actions/TestRecipeAction.cs` (Key, Required list, Reward; if all required present → remove each + add reward), XML docs, `[CreateAssetMenu]`.
- [ ] T015 [US4] Make T011 GREEN (depends on T012-T014).

---

## Phase 8: Polish & Finalize

- [ ] T016 Bump `com.faolline.graphcore/package.json` version `0.4.0` → `0.5.0` (semver MINOR).
- [ ] T017 [P] Verify XML docs on ALL new public API (the 9 BaseContext methods) and `[GraphCore]` prefix on every misuse warning.
- [ ] T018 Full batchmode EditMode run of graphcore + graphTest (editor closed, no `-quit`), all green; verify the results XML; record totals. Re-confirm SC-002/SC-004/SC-006.
- [ ] T019 [P] Validate `quickstart.md` snippets compile and behave as documented; fix drift if any.

---

## Dependencies & Execution Order

- **Setup (T001)** → no deps.
- **US1 (T002→T003)** → the store; MVP.
- **US2 (T004/T005→T006)** → after US1 (durability needs the store).
- **US3 (T007→T008)** → after US1 (notifications fire from the store ops).
- **Back-compat (T009, T010)** → after US1+US2+US3 (full surface present).
- **US4 (T011→T012/T013/T014→T015)** → after US1 (conditions/recipe read the store). T012-T014 are independent files ([P]).
- **Polish (T016–T019)** → last; T018 after everything.

## Parallel Opportunities

- Test-authoring T002 / T004 / T005 / T007 / T009 / T011 are different files ([P]) but each precedes its own implementation.
- T012, T013, T014 (graphTest authoring classes) are independent files — run together, before T015.
- T017 [P] and T019 [P] independent polish.

## Implementation Strategy

### MVP (US1 + US2)

1. T001 setup.
2. US1: T002 (RED) → T003 (GREEN) — the store.
3. US2: T004/T005 (RED) → T006 (GREEN) — durability + save surface.
4. Back-compat gate T010 (existing suite green). **STOP & VALIDATE** — durable, persistable set state.

### Incremental

5. US3 (T007→T008) — change notifications (Reactive hook).
6. US4 (T011→T012/T013/T014→T015) — authoring + recipe in graphTest.
7. Finalize (T016–T019) — 0.5.0, docs, full green.

## Notes

- Commit after each GREEN task or logical group.
- Non-breakage gate (T010) is non-negotiable: the entire pre-existing suite passes UNMODIFIED.
- No `MonoBehaviour`/`UnityEvent` in Runtime; one class per file; `[GraphCore]` prefix; XML docs on new public API.
- `GetAllParameters` signature/return shape is frozen — collections go through the parallel `GetAllCollections`.
