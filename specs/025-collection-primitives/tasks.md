---
description: "Task list for 025-collection-primitives (3 additive graphstandard collection primitives + reactive-hosting pattern doc)"
---

# Tasks: graphstandard universal collection primitives + reactive-hosting pattern (slice 6)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD — tests before code), all EditMode. Batchmode (no `-quit`; re-run after a source change;
verify XML). Branch `025-collection-primitives` (stacks on master). **graphcore + gameflow + graphTest UNTOUCHED;
graphstandard append-only (only new files).** Three new SO types live in the existing
`com.faolline.graphstandard.Runtime` asmdef.

## Phase 1: US1 — record membership from a node (Priority: P1) 🎯 MVP

**Goal**: a node can record a value into a context collection via a stock action; idempotent; empty-config no-op.

**Independent test**: `AddToCollectionAction{K,V}.Execute(ctx)` ⇒ `ctx.CollectionContains(K,V)`; twice ⇒ count 1;
empty key/value ⇒ no change.

- [X] T001 [P] [US1] In `com.faolline.graphstandard/Tests/EditMode/Collections/CollectionPrimitivesTests.cs` (new file + an `EditMode/Collections` folder): tests for `AddToCollectionAction` — after `Execute(ctx)` with (`"completed"`,`"a"`) the collection contains `"a"` (INV-1); a second `Execute` leaves `CollectionCount("completed") == 1` (INV-1 idempotent); `Execute` with empty key OR empty value leaves the collection unchanged (INV-2). Use a plain `BaseContext`/`GameFlowContext` and `ScriptableObject.CreateInstance<AddToCollectionAction>()`. Confirm RED (type missing).
- [X] T002 [US1] Implement `com.faolline.graphstandard/Runtime/Actions/AddToCollectionAction.cs`: `BaseAction` with `[SerializeField] string _collectionKey/_value` + `CollectionKey`/`Value` props; `Execute` calls `context.AddToCollection(_collectionKey, _value)` only when both are non-empty (`!string.IsNullOrWhiteSpace`). `[CreateAssetMenu(menuName="GraphStandard/Actions/Add To Collection", fileName="AddToCollectionAction")]`; XML docs. Confirm T001 GREEN.

## Phase 2: US2 — gate an edge on collection state (Priority: P1)

**Goal**: stock conditions read a collection (contains; count ≥ threshold), with absent-key/zero-threshold edges.

**Independent test**: `CollectionContainsCondition` true iff present; `CollectionCountAtLeastCondition` true iff
`Count >= threshold` (threshold 0 always true; positive threshold false on absent key).

- [X] T003 [P] [US2] Add to `CollectionPrimitivesTests.cs`: `CollectionContainsCondition{K,V}.Evaluate(ctx)` true when `K` contains `V`, false when absent (INV-3); `CollectionCountAtLeastCondition{K,N}.Evaluate(ctx)` true when `CollectionCount(K) >= N`, false below; threshold 0 true on an absent key; positive threshold false on an absent key (INV-4). Confirm RED.
- [X] T004 [P] [US2] Implement `com.faolline.graphstandard/Runtime/Conditions/CollectionContainsCondition.cs`: `BaseCondition` with `_collectionKey/_value` + props; `Evaluate` ⇒ `context.CollectionContains(_collectionKey, _value)`. `[CreateAssetMenu(menuName="GraphStandard/Conditions/Collection Contains", fileName="CollectionContainsCondition")]`; XML docs.
- [X] T005 [P] [US2] Implement `com.faolline.graphstandard/Runtime/Conditions/CollectionCountAtLeastCondition.cs`: `BaseCondition` with `_collectionKey` + `int _threshold` + props; `Evaluate` ⇒ `context.CollectionCount(_collectionKey) >= _threshold`. `[CreateAssetMenu(menuName="GraphStandard/Conditions/Collection Count At Least", fileName="CollectionCountAtLeastCondition")]`; XML docs. Confirm T003 GREEN.

## Phase 3: US3 — host a reactive progression on the shared context (Priority: P1)

**Goal**: prove the end-to-end pattern — action writes ids → evaluator over the same ctx derives a k-of-N node
Locked→Available — with only the two-line `OnCollectionChanged → Reevaluate` bridge.

**Independent test**: downstream node `requiredCounts=k`; not Available before the k-th recorded prerequisite,
Available at/after.

- [X] T006 [US3] Add to `CollectionPrimitivesTests.cs` the pattern test (INV-5): build a `BaseGraph` (a test graph or the graphstandard builder) with prerequisite nodes `p1..p3` and a downstream `exit`, edges `p*→exit`; each `p*` carries an `AddToCollectionAction{ "completed", p* .Id }`; a `ReactiveEvaluator(graph, ctx, "completed", requiredCounts:{["exit"]=2})` with `ctx.OnCollectionChanged("completed", _ => ev.Reevaluate())` and a captured `OnNodeAvailable`. Execute the actions one at a time: after 1 record `exit` is NOT Available; after the 2nd, `exit` IS Available (state + event). Confirm GREEN (all three SO types now exist).

## Phase 4: Polish

- [X] T007 Run the ENTIRE suite via batchmode: graphstandard EditMode (prior + the new collection tests) green, AND graphcore + gameflow EditMode green, AND PlayMode (9) green (graphcore/gameflow/graphTest untouched, INV-6). Record totals.
- [X] T008 [P] Bump `com.faolline.graphstandard/package.json` `0.4.0 → 0.5.0`; update `README.md` (the three primitives + a short "hosting a reactive progression on the shared context" pattern note linking the ReactiveEvaluator + the gameflow Boot seam) and `CHANGELOG.md` (`0.5.0`).
- [X] T009 [P] Verify `[GraphStandard]` prefix (on any warning), one class per file, `[CreateAssetMenu]` on all three, XML docs, and append-only (no changed signatures anywhere; graphcore + gameflow + graphTest untouched; graphTest fixtures still present).

## Dependencies

- **US1 (T001→T002)**, **US2 (T003→T004,T005)**, **US3 (T006)** — US3 depends on all three SO types existing
  (T002, T004, T005). Tests T001/T003 can be written together (same file) before any impl.
- **Polish (T007–T009)** last.

## Implementation strategy

- Tests-first per story in one test file; implement the three small SO types (each ~30 lines, mirroring the
  graphTest fixtures minus the comparison-operator generality); then the end-to-end pattern test ties them to the
  existing `ReactiveEvaluator` + the slice-5 Boot seam. Primitives carry no engine dependency (Constitution II) —
  the bridge lives in consumer/test code, not in the action.
