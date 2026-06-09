---
description: "Task list for 017-threshold-join (P4 — generic k-of-N threshold in ReactiveEvaluator)"
---

# Tasks: P4 — Generic threshold Join (k-of-N prerequisites)

**Input**: Design documents from `specs/017-threshold-join/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD). EditMode only. Batchmode (editor CLOSED; `-runTests -testPlatform EditMode`
WITHOUT `-quit`; re-run once after source changes; verify the XML — see memory).

**Organization**: by user story. Branch `017-threshold-join` (P1+P2+P3 on master). graphcore UNTOUCHED.

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

`com.faolline.graphstandard/`. Repository-relative.

---

## Phase 1: User Story 1 + 2 — Threshold derivation (Priority: P1) 🎯 MVP

**Goal**: per-node required count k; Available when ≥k prerequisites Completed; default k=N (AND); spectrum + boundaries.

**Independent Test**: D requires A,B,C — default needs all 3; k=2 needs any 2; k=1 any 1; k≤0 ungated; k>N never auto.

### Tests (write FIRST) ⚠️

- [X] T001 [P] [US1] Write `ReactiveThresholdJoinTests` in `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveThresholdJoinTests.cs` covering: default (no config) needs ALL prerequisites (INV-1); k=2 over A,B,C ⇒ Available after any two (INV-2); k=1 ⇒ OR, Available after the first (INV-3); k=N ⇒ identical to AND (INV-3); k=0/negative ⇒ ungated Available (INV-4); k>N ⇒ never auto-available, Locked even with all prereqs done, no error (INV-4); a configured count for an unknown id is ignored (INV-6). Confirm RED.

### Implementation

- [X] T002 [US1] In `com.faolline.graphstandard/Runtime/Reactive/ReactiveEvaluator.cs`: add an optional 4th ctor parameter `IReadOnlyDictionary<string,int> requiredCounts = null` (copy into a private `_requiredCounts`); refine `DeriveState` to compute `completed = count of prerequisites in the completed-set`, `k = _requiredCounts.TryGetValue(id)? value : N`, and return Available iff `completed >= k` (Completed still takes precedence). XML docs on the new ctor param/behavior; `[GraphStandard]` for any misuse log. Confirm T001 GREEN.

**Checkpoint**: k-of-N derivation works; default AND preserved.

---

## Phase 2: User Story 3 — Lifecycle integration (Priority: P2)

**Goal**: the threshold is honored by cascade, events, Start, and reversible Reevaluate.

**Independent Test**: a region node k=2 over 3 members fires Available exactly when the 2nd member completes; step-back below 2 re-locks it.

### Tests (write FIRST) ⚠️

- [X] T003 [P] [US3] Add to `ReactiveThresholdJoinTests` (or a sibling): a region node with k=2 over three members — OnNodeAvailable fires exactly once when the second member is MarkCompleted (INV-5); after un-completing a member and Reevaluate the region is Locked again (INV-5 reversible); a full default-AND scenario matches P3 (INV-1/INV-7). Confirm RED then GREEN (no new impl expected — DeriveState chokepoint already covers it; if a gap appears, fix it in DeriveState only).

---

## Phase 3: Back-compat + Finalize

- [X] T004 Run the ENTIRE existing 602-test suite (graphcore + graphTest + graphstandard P3) UNCHANGED via batchmode; confirm green (default AND, SC-002). Record totals.
- [X] T005 Bump `com.faolline.graphstandard/package.json` version `0.1.0` → `0.2.0` (semver MINOR).
- [X] T006 [P] Verify XML docs on the new ctor parameter/behavior; confirm graphcore untouched (SC-005); validate `quickstart.md` snippets.
- [X] T007 Full batchmode EditMode run (no `-quit`), verify the XML: all green (602 prior + new threshold tests).

---

## Dependencies & Execution Order

- US1/US2 (T001→T002) → the core.
- US3 (T003) → after T002 (likely no new impl; verifies the chokepoint).
- Back-compat/Finalize (T004-T007) → last.

## Notes

- Only `ReactiveEvaluator.cs` + `package.json` change; one new test file. graphcore + `ReactiveNodeState` UNTOUCHED.
- The new ctor parameter is OPTIONAL ⇒ all P3 callers/tests keep working with default AND (the non-breakage gate).
- `[GraphStandard]` prefix; one class per file; XML docs.
