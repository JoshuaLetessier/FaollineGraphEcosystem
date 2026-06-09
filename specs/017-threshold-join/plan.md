# Implementation Plan: P4 — Generic threshold Join (k-of-N prerequisites)

**Branch**: `017-threshold-join` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-threshold-join/spec.md`

## Summary

A surgical, additive change to `com.faolline.graphstandard`'s **`ReactiveEvaluator`** (P3): the
prerequisite rule generalizes from "ALL Completed" (AND) to a per-node **required count k** — a node is
**Available** when the number of its Completed prerequisites is ≥ k (and it is not itself Completed). k is
supplied as **optional evaluator configuration** (a node-id → required-count map on a new optional
constructor parameter); when a node has no configured count, k **defaults to its prerequisite count N**,
which is exactly P3's AND. One parameter spans the spectrum: k=N (AND), k=1 (OR), 1<k<N (N-of-M), k≤0
(ungated), k>N (never auto-available). The threshold flows through every existing behavior for free because
they all route through the single `DeriveState` method. graphcore is **untouched**; the existing public API
stays source-compatible. **graphstandard 0.1.0 → 0.2.0 (semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0.

**Primary Dependencies**: `com.faolline.graphcore` 0.5.0 (unchanged consumer). No new dependency.

**Storage**: N/A — the threshold is in-memory evaluator configuration; completion state remains the P2
completed-set (unchanged).

**Testing**: Unity Test Framework, EditMode only (headless). Run via batchmode (editor closed; no `-quit`;
re-run after source change; verify XML). The existing 602-test suite must stay green (default AND).

**Target Platform**: Any Unity runtime; Editor 6000.0+.

**Project Type**: Additive enhancement to the graphstandard reactive engine. No new package/assembly/type.

**Performance Goals**: Derivation stays O(prerequisites) per node — count completed prerequisites and
compare to k. No extra allocation when no thresholds are configured (the default path).

**Constraints**: graphcore untouched (SC-005). `ReactiveEvaluator`'s existing public surface stays
source-compatible (new optional ctor param). Universal abstractions only. `[GraphStandard]` prefix; one
class per file; XML docs on new public API.

**Scale/Scope**: One new optional ctor parameter + one private field + a tweak to `DeriveState` in
`ReactiveEvaluator.cs`; one new EditMode test file; package version bump.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore untouched. graphstandard change is additive: a new optional ctor parameter (existing 3-arg calls bind to default `null`), a private field, and a `DeriveState` refinement that reduces to the P3 AND when no count is configured. 0.1.0 → 0.2.0. The 602-test suite stays green by default-AND. |
| II. Universal Abstractions Only | ✅ PASS | A k-of-N threshold over prerequisites is universal to dependency graphs. Neutral naming; zero domain vocabulary. |
| III. Specification-First | ✅ PASS | `spec.md` approved (checklist all-green) before this plan. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Failing EditMode tests first for OR, N-of-M, default-AND-unchanged, k≤0, k>N, lifecycle integration, game scenario. (Compile-against-API ⇒ gate is GREEN + non-regression via batchmode.) |
| V. Simplicity (YAGNI) | ✅ PASS | One threshold parameter generalizes the join (no separate AND/OR/N-of-M node types); supplied as a config map (no new serialized node type, no editor UI). Default-to-N means zero behavior change when unused. |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context state added; the completed-set (P2) is unchanged. |
| VII. Cross-lib via SubGraph only | ✅ PASS | Pure in-lib enhancement; no new cross-lib mechanism. |
| Dev: no MonoBehaviour/UnityEvent; prefix; one class per file; XML docs | ✅ PASS | Pure C#; `[GraphStandard]` prefix; XML docs on the new ctor param/behavior. |

**Result**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/017-threshold-join/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1..R3
├── data-model.md        # Phase 1 — config, derivation rule, invariants
├── quickstart.md        # Phase 1 — author walkthrough
├── contracts/
│   └── public-api.md    # Phase 1 — the additive public surface + invariants
└── checklists/
    └── requirements.md  # from /speckit-specify (all green)
```

### Source Code (repository root)

```text
com.faolline.graphstandard/
├── package.json                                   # version 0.1.0 → 0.2.0 (MINOR)
├── Runtime/Reactive/
│   └── ReactiveEvaluator.cs                       # + optional ctor param IReadOnlyDictionary<string,int>
│                                                  #   requiredCounts; private _requiredCounts; DeriveState
│                                                  #   uses completed-prereq count vs. per-node threshold
│                                                  #   (default = prereq count N). All other members unchanged.
└── Tests/EditMode/Reactive/
    └── ReactiveThresholdJoinTests.cs              # OR / N-of-M / default-AND / k<=0 / k>N / lifecycle / region scenario

# graphcore/ : UNCHANGED.  ReactiveNodeState.cs : UNCHANGED.
```

**Structure Decision**: Smallest possible change — the threshold lives entirely inside `ReactiveEvaluator`
as optional configuration and a refined `DeriveState`. Because cascade/events/Start/Reevaluate all derive
state through that one method, they honor the threshold with no further change. No new type, no graphcore
edit, no editor work.

## Phase 0 — Research

See [research.md](research.md): **R1** threshold as evaluator config (ctor map) vs. a serialized node field
/ new Join node type; **R2** default = N (AND) for back-compat + the k≤0 / k>N boundary semantics; **R3**
why no other code path needs changing (single `DeriveState` chokepoint).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): the `requiredCounts` config, the refined derivation rule, and invariants.
- [contracts/public-api.md](contracts/public-api.md): the additive public surface (one optional ctor
  parameter) + testable invariants and acceptance→invariant traceability.
- [quickstart.md](quickstart.md): how an author configures AND / OR / N-of-M per node.

## Implementation Sequencing (TDD)

1. **US1/US2 — Threshold derivation**: failing `ReactiveThresholdJoinTests` (OR, N-of-M, default-AND,
   k≤0, k>N) → add the optional `requiredCounts` ctor param + `_requiredCounts` field; refine `DeriveState`
   to compare completed-prereq count against the per-node threshold (default N).
2. **US3 — Lifecycle integration**: extend the test (cascade fires the available event at the threshold;
   step-back re-locks below threshold; default path identical to P3) → confirm no other change needed.
3. **Back-compat**: run the entire existing 602-test suite unchanged (default AND) — must be green.
4. **Finalize**: bump `package.json` to 0.2.0; XML docs on the new ctor param/behavior; batchmode = full
   suite green.

## Complexity Tracking

> No constitution violations — section intentionally empty.
