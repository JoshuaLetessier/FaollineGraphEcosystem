# Implementation Plan: graphstandard universal collection primitives + reactive-hosting pattern (slice 6)

**Branch**: `025-collection-primitives` | **Date**: 2026-06-10 | **Spec**: [spec.md](spec.md)

**Input**: `specs/025-collection-primitives/spec.md`

## Summary

Three new **universal** ScriptableObject primitives in graphstandard, plus a documented composition pattern:

- `AddToCollectionAction : BaseAction` — on `Execute`, `context.AddToCollection(key, value)` (no-op on empty
  key/value); `[CreateAssetMenu]` "GraphStandard/Actions/Add To Collection".
- `CollectionContainsCondition : BaseCondition` — `Evaluate` ⇒ `context.CollectionContains(key, value)`;
  `[CreateAssetMenu]` "GraphStandard/Conditions/Collection Contains".
- `CollectionCountAtLeastCondition : BaseCondition` — `Evaluate` ⇒ `context.CollectionCount(key) >= threshold`
  (threshold 0 ⇒ always true); `[CreateAssetMenu]` "GraphStandard/Conditions/Collection Count At Least".

All operate purely on graphcore's universal collection API on `BaseContext`. A quickstart documents the
**reactive-hosting pattern**: a Linear flow node carries `AddToCollectionAction` writing ids into a completed-set;
a `ReactiveEvaluator` over the **same** context (k-of-N via `requiredCounts`) derives unlocks, bridged by a
two-line `OnCollectionChanged(key, _ => evaluator.Reevaluate())`; a Linear edge may gate via
`CollectionCountAtLeastCondition`. graphcore + gameflow untouched; graphTest fixtures untouched; graphstandard
`0.4.0 → 0.5.0`; existing suites stay green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: `com.faolline.graphcore` (`BaseAction`,
`BaseCondition`, `BaseContext.AddToCollection`/`CollectionContains`/`CollectionCount`/`OnCollectionChanged`),
and graphstandard's own `ReactiveEvaluator` (used only by the integration test + the quickstart, not by the
primitives). **Storage**: none. **Testing**: NUnit EditMode — action adds + idempotent + empty no-op; contains
true/false; count-at-least true/false + zero-threshold + absent-key; one end-to-end pattern test (action writes
ids → evaluator over same ctx with `OnCollectionChanged → Reevaluate` derives a k-of-N node Locked→Available).
**Target Platform**: Unity runtime + Editor. **Project Type**: standard-lib additive (3 new SO types).
**Constraints**: graphcore + gameflow untouched; graphTest untouched; graphstandard append-only (only new
files); `[GraphStandard]` prefix; one class per file; `[CreateAssetMenu]`; XML docs. **Scope**: 3 runtime files
+ EditMode tests + README/CHANGELOG + package bump.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore untouched. graphstandard additive `0.4.0 → 0.5.0`, only new files; graphTest fixtures kept (FR-009). All existing suites stay green. |
| II. Universal Abstractions Only | ✅ PASS | The primitives are generic collection-of-strings write/read — no domain vocabulary. "Completed-set" is a usage example in docs/tests, not coded vocabulary. |
| III. Specification-First | ✅ PASS | spec.md approved (16/16). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Tests-first: add/idempotent/no-op; contains; count-at-least + edges; end-to-end pattern. All EditMode. |
| V. Simplicity (YAGNI) | ✅ PASS | Add-only; contains + count-at-least only; no remove/clear, no comparison-operator enum (the graphTest count condition's generality is not promoted — only `>=` threshold, which the pattern needs). |
| VI. Typed Context Contract | ✅ PASS (N/A) | Primitives take a `BaseContext`; keys are author-configured strings. |
| VII. Cross-lib via SubGraph only | ✅ PASS | No cross-lib mechanism; graphstandard depends only on graphcore. |
| Dev standards | ✅ PASS | `[GraphStandard]` prefix (only needed on any warning log); one class per file; `[CreateAssetMenu]`; XML docs. |

**Result**: PASS — no violations, no deviations.

## Project Structure

```text
com.faolline.graphstandard/
├── package.json                                              # 0.4.0 → 0.5.0
├── Runtime/
│   ├── Actions/AddToCollectionAction.cs                      # NEW
│   └── Conditions/
│       ├── CollectionContainsCondition.cs                    # NEW
│       └── CollectionCountAtLeastCondition.cs                # NEW
└── Tests/EditMode/Collections/
    └── CollectionPrimitivesTests.cs                          # NEW (US1+US2+US3)

# com.faolline.graphcore/, com.faolline.graphgameflow/, com.faolline.graphTest/ : UNCHANGED.
```

**Structure Decision**: three additive ScriptableObject types in graphstandard Runtime (mirroring the graphTest
fixtures into the real lib, minus the unneeded generality), plus one EditMode test file covering all three user
stories. The `Runtime/Actions/` and `Runtime/Conditions/` folders are new (graphstandard has none yet); they
sit inside the existing `com.faolline.graphstandard.Runtime` asmdef (no new assembly).

## Phase 0 — Research

See [research.md](research.md): R1 promote-not-reference (the real lib owns the primitives; graphTest fixtures
stay as test reference); R2 count-at-least, not a comparison-operator enum (YAGNI — only `>=` is needed; the
graphTest `ComparisonOperator` generality is not promoted); R3 the bridge is `OnCollectionChanged → Reevaluate`
(graphcore already raises collection-change events — no signal convention, no engine change); R4 empty/zero
semantics (empty key/value action = no-op; absent collection = empty for both conditions; threshold 0 = always).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **Tests (test-first)** in `Tests/EditMode/Collections/CollectionPrimitivesTests.cs`:
   - **US1**: `AddToCollectionAction` with (`K`,`V`) → after `Execute(ctx)`, `ctx.CollectionContains(K,V)`;
     running twice keeps `CollectionCount(K) == 1`; empty key OR empty value → no change.
   - **US2**: `CollectionContainsCondition`(`K`,`V`) → `Evaluate` true iff `K` contains `V`;
     `CollectionCountAtLeastCondition`(`K`, `N`) → true iff `CollectionCount(K) >= N`; threshold 0 true on an
     absent key; positive threshold false on an absent key.
   - **US3 (end-to-end pattern)**: build a graph with prerequisite nodes `p1..pN` each carrying an
     `AddToCollectionAction` writing its id into `"completed"`, and a downstream node `d` with `requiredCounts[d]=k`;
     a `ReactiveEvaluator(graph, ctx, "completed")` with `ctx.OnCollectionChanged("completed", _ => ev.Reevaluate())`;
     fire the actions one by one → `d` is not Available before the k-th, Available at/after the k-th (assert via
     `GetState`/`OnNodeAvailable`). Confirm RED (the three SO types don't exist yet).
2. **Implement** the three SO types (`ScriptableObject.CreateInstance` in tests; `[CreateAssetMenu]` on each).
   Confirm GREEN.
3. **Finalize**: full suite via batchmode (graphstandard EditMode + graphcore + gameflow EditMode green, PlayMode
   green); bump `0.5.0`; README (the three primitives + the reactive-hosting pattern note) + CHANGELOG; verify
   prefix / one-class-per-file / XML / append-only.

## Complexity Tracking

> No violations — empty.
