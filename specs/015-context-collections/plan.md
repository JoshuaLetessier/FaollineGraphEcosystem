# Implementation Plan: P2 — Context collections (named string-sets)

**Branch**: `015-context-collections` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-context-collections/spec.md`

## Summary

GraphCore's `BaseContext` gains **named string-set collections** beside its four scalar parameters: a
graph can add/remove/test/count/enumerate/clear set-valued state (solved-set, inventory, collected). The
sets live in a **separate, lazily-allocated bucket** (`_collections`) with their **own keyspace**,
independent of `_params`. Unlike P1 signals (transient), collections are **durable state**: `DeepClone`
deep-copies them (so step-back restores exact membership) and a new parallel **`GetAllCollections()`**
exposes them for saving, while `GetAllParameters()` stays scalar-only and unchanged. A real membership
change fires a per-key **change notification** (mirroring the scalar `OnParameterChanged` pattern) for
future reactive consumers; idempotent operations are silent. Collections are **global-only** — they ignore
the 0.3.0 local-context overlay. The membership/count conditions and the consume-set→produce **recipe**
action are exercised in **graphTest** (to be promoted to `graphstandard` later). All additions are
append-only → graphcore **0.4.0 → 0.5.0 (semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0 (`com.faolline.graphcore` `unity: 6000.0`).

**Primary Dependencies**: none new. Changes are confined to the graphcore Runtime assembly
(`BaseContext.cs`) plus authoring classes in the graphTest sandbox.

**Storage**: collections are **durable**: captured by history `DeepClone`/`CopyValuesFrom` and exposed for
save via `GetAllCollections()`. The save layer (downstream) composes scalar + collection snapshots; no new
core dependency.

**Testing**: Unity Test Framework, **EditMode only** (headless — Principle IV). Run via Unity 6000.3
batchmode (editor closed; `-runTests -testPlatform EditMode` WITHOUT `-quit`; re-run once after source
changes; verify the results XML — see memory). Exercised additionally in `com.faolline.graphTest`.

**Target Platform**: Any Unity runtime; Editor 6000.0+.

**Project Type**: Foundation library evolution — additive change to `BaseContext`. No new package, no new
assembly, no new public type in core (only new methods).

**Performance Goals**: Zero added cost when unused — both new dictionaries are lazily allocated, so a
context that never touches collections allocates nothing and runs identically to 0.4.0. Operations are a
single dictionary + `HashSet` access.

**Constraints**: Foundation Stability (Principle I) NON-NEGOTIABLE — every change append-only and
semver-MINOR; the **entire existing EditMode suite (560 tests, incl. P1 signals) MUST stay green
unmodified** as the non-breakage gate (SC-002). `GetAllParameters` return shape frozen. No
`MonoBehaviour`/`UnityEvent`; `[GraphCore]` prefix; one class per file; XML docs on new public API.
`INodeExecutor` untouched.

**Scale/Scope**: Small/surgical — two new private fields + ~9 new public methods on `BaseContext`, plus
extensions to `DeepClone` and the internal `CopyValuesFrom`. New EditMode test files in graphcore; three
authoring classes + one test file in graphTest.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | All additions append-only: new private fields + new public methods on `BaseContext`; `DeepClone`/`CopyValuesFrom` extended additively; `GetAllParameters` unchanged (a parallel `GetAllCollections` is added). No public signature removed/changed. No-collections path identical to 0.4.0. Version 0.4.0 → 0.5.0. Existing suite green = the gate. |
| II. Universal Abstractions Only | ✅ PASS | "A named set of values in the blackboard" is universal to graph systems. Neutral naming (`AddToCollection`, `CollectionContains`…); zero domain vocabulary ("inventory"/"puzzle" stay in graphTest/downstream). |
| III. Specification-First | ✅ PASS | `spec.md` approved (checklist all-green, no markers) before this plan. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Red-Green-Refactor: failing EditMode tests first for set ops, set semantics, notifications, durability/step-back, save-surface separation, overlay-independence, back-compat. (C#/Unity tests must compile against the new API, so the gate is GREEN + non-regression via batchmode rather than a compile-failing RED run — noted in plan.) |
| V. Simplicity (YAGNI) | ✅ PASS | String-set only (no lists/multisets/ordering, no non-string elements); global-only (no overlay routing); a separate bucket (not encoded into `_params`). Each restriction is the simplest thing that satisfies the evidenced need (solved-set/inventory). |
| VI. Typed Context Contract | ✅ PASS | Collections are managed by `BaseContext` itself, so `base.DeepClone()` copies them for every subclass automatically; subclasses keep overriding only `CreateCloneInstance`. No raw scalar-key literals introduced; collection keyspace is separate. |
| VII. Cross-lib via SubGraph only | ✅ PASS | Pure context state; no new cross-graph/cross-lib mechanism. |
| Dev: no MonoBehaviour/UnityEvent; `[GraphCore]` prefix; one class per file; XML docs | ✅ PASS | Pure C# (`HashSet`, `Action<string>`); new public API gets XML docs; misuse warnings carry `[GraphCore]`. |

**Result**: PASS — no violations, no Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/015-context-collections/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1..R6
├── data-model.md        # Phase 1 — fields, methods, invariants
├── quickstart.md        # Phase 1 — author + integrator walkthrough
├── contracts/
│   └── public-api.md    # Phase 1 — authoritative new public surface + invariants
└── checklists/
    └── requirements.md  # from /speckit-specify (all green)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── package.json                                  # version 0.4.0 → 0.5.0 (MINOR)
├── Runtime/Graph/
│   └── BaseContext.cs                            # + _collections (Dictionary<string,HashSet<string>>),
│                                                 #   _collectionSubs (Dictionary<string,List<Action<string>>>);
│                                                 #   AddToCollection/RemoveFromCollection/CollectionContains/
│                                                 #   CollectionCount/GetCollection/ClearCollection;
│                                                 #   OnCollectionChanged/OffCollectionChanged; GetAllCollections;
│                                                 #   DeepClone + CopyValuesFrom extended (deep-copy collections).
│                                                 #   _params, GetAllParameters, local overlay: UNCHANGED.
└── Tests/EditMode/
    ├── Collections/
    │   ├── CollectionStoreTests.cs               # add/remove/contains/count/enumerate/clear; set semantics;
    │   │                                         #   independent keyspace; null/empty guards (US1)
    │   ├── CollectionNotificationTests.cs        # fire on real change; silent on idempotent; re-entrant; off (US3)
    │   └── CollectionDurabilityTests.cs          # DeepClone independence; GetAllCollections snapshot;
    │                                             #   GetAllParameters excludes collections; CopyValuesFrom (US2)
    └── Execution/
        ├── CollectionStepBackTests.cs            # runner GoBack restores exact membership (US2)
        └── CollectionBackCompatTests.cs          # overlay-independence; no-collections identical to 0.4.0

com.faolline.graphTest/                           # authoring exercise (FR-013), to promote to graphstandard
├── Runtime/Conditions/TestCollectionContainsCondition.cs   # membership gate
├── Runtime/Conditions/TestCollectionCountCondition.cs      # count-threshold gate (reuse ComparisonOperator)
├── Runtime/Actions/TestRecipeAction.cs                     # consume required set → add reward
└── Tests/EditMode/Runtime/CollectionExerciseTests.cs       # US4: gated edges + recipe end-to-end
```

**Structure Decision**: No new package, assembly, or public core type — a surgical, additive evolution of
`BaseContext`. Collections get their own bucket + keyspace (no entanglement with the scalar store) and are
deliberately kept out of the local-context overlay. The authoring nodes live in graphTest exactly like the
014 `TestSignalPayloadCondition`, earmarked for promotion to the future `graphstandard` lib.

## Phase 0 — Research

See [research.md](research.md): **R1** separate `_collections` bucket vs. encoding inside `_params`;
**R2** public API surface + neutral naming + change-notification handler signature; **R3** the parallel
`GetAllCollections()` save accessor + read-only return shape vs. changing `GetAllParameters`; **R4**
history `DeepClone`/`CopyValuesFrom` deep-copy + Principle VI subclass inheritance; **R5** global-only
(ignore the local overlay) vs. overlaying collections; **R6** graphTest authoring (membership/count
conditions + recipe action), to promote to `graphstandard`.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): exact field/method changes to `BaseContext`; set-semantics, durability,
  overlay-independence, and notification invariants.
- [contracts/public-api.md](contracts/public-api.md): authoritative new public surface + testable
  invariants (set ops, notifications, save separation, history independence, back-compat) and
  acceptance→invariant traceability.
- [quickstart.md](quickstart.md): how an author mutates/queries a collection, subscribes to changes,
  reads the save snapshot, and uses the graphTest membership/count/recipe nodes.

## Implementation Sequencing (TDD, by user-story priority)

1. **US1 — Collection store** (P1): failing `CollectionStoreTests` → implement `_collections`,
   `AddToCollection`/`RemoveFromCollection`/`CollectionContains`/`CollectionCount`/`GetCollection`/
   `ClearCollection`, set semantics, independent keyspace, null/empty guards.
2. **US2 — Durability** (P1): failing `CollectionDurabilityTests` + `CollectionStepBackTests` →
   `GetAllCollections`, extend `DeepClone` (deep-copy) and `CopyValuesFrom` (clear+rebuild); confirm
   `GetAllParameters` unchanged and collections survive step-back as independent copies.
3. **US3 — Notifications** (P2): failing `CollectionNotificationTests` → `_collectionSubs`,
   `OnCollectionChanged`/`OffCollectionChanged`, fire-on-real-change only, snapshot-on-fire.
4. **Back-compat + overlay-independence**: `CollectionBackCompatTests` → collections ignore
   `BeginLocalContext`/`EndLocalContext`; no-collections context identical to 0.4.0; then run the entire
   pre-existing suite unmodified (must be green).
5. **US4 — graphTest authoring** (P2 / FR-013): `TestCollectionContainsCondition`,
   `TestCollectionCountCondition`, `TestRecipeAction` + `CollectionExerciseTests` (gated edges + recipe).
6. **Finalize**: bump `package.json` to 0.5.0; XML docs on all new public API; batchmode run = full suite
   green; semver assessment note.

## Complexity Tracking

> No constitution violations — section intentionally empty.
