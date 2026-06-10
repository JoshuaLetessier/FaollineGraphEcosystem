---
description: "Task list for 026-reactive-relock-event (additive OnNodeLocked event on ReactiveEvaluator + README clarity)"
---

# Tasks: ReactiveEvaluator re-lock event + reactive-hosting doc clarity (slice 7)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD), EditMode. Batchmode (no `-quit`; re-run after a source change; verify XML). Branch
`026-reactive-relock-event` (stacks on master). **graphcore + gameflow UNTOUCHED; graphstandard append-only
(one new event reusing `EmitFor`).**

## Phase 1: US1 — react to a node re-locking (Priority: P1) 🎯 MVP

**Goal**: a symmetric `OnNodeLocked` event firing on backward transitions and initial emission.

**Independent test**: drive Locked→Available→Locked; the event fires on the backward step and on initial
emission, not on an unchanged node.

- [X] T001 [US1] In `com.faolline.graphstandard/Tests/EditMode/Reactive/ReactiveRelockEventTests.cs` (new): (a) a graph `A,B → D` with `requiredCounts{D=2}`; complete A,B (`MarkCompleted`) → D Available; `ctx.RemoveFromCollection("completed","B")` + `Reevaluate()` → `OnNodeLocked` fires once for `D`, `GetState("D")==Locked` (INV-1); (b) on a fresh evaluator, `Start()` raises `OnNodeLocked` for an initially-Locked node and NOT for an Available one (INV-2); (c) a `Reevaluate` that leaves a node Available raises no `OnNodeLocked` for it (INV-3). Confirm RED (event missing).
- [X] T002 [US1] In `com.faolline.graphstandard/Runtime/Reactive/ReactiveEvaluator.cs`: add `public event Action<string> OnNodeLocked;` (XML docs, next to the other two events) and one branch in `EmitFor`: `else if (state == ReactiveNodeState.Locked) OnNodeLocked?.Invoke(nodeId);`. No other change. Confirm T001 GREEN.

## Phase 2: US2 — documentation clarity (Priority: P2)

- [X] T003 [US2] In `com.faolline.graphstandard/README.md`, restructure the "Hosting a reactive progression" section: lead with owning the evaluator + `MarkCompleted`; present the `AddToCollectionAction` + `OnCollectionChanged → Reevaluate` bridge as the **alternative** for when a flow writes the set; add an explicit "call `MarkCompleted` **or** bridge — not both (double-evaluation)" caveat; document the new `OnNodeLocked` event in the reactive section. (FR-004)

## Phase 3: Polish

- [X] T004 Run the ENTIRE suite via batchmode: graphstandard EditMode (prior + the new re-lock tests) green, AND graphcore + gameflow EditMode green, AND PlayMode (9) green (INV-4/INV-5). Record totals.
- [X] T005 [P] Bump `com.faolline.graphstandard/package.json` `0.5.0 → 0.6.0`; update `CHANGELOG.md` (`0.6.0` — `OnNodeLocked` event + the hosting-doc clarification).
- [X] T006 [P] Verify XML docs on the new event, append-only (no changed signatures; `OnNodeAvailable`/`OnNodeCompleted`/derivation unchanged; graphcore + gameflow untouched).

## Dependencies

- T001 → T002 (test-first). T003 doc independent. Polish (T004–T006) last.

## Implementation strategy

- One event routed through the existing `EmitFor` choke point → identical firing semantics to the other two
  events, zero new state. Doc restructure addresses the only round-4 confusion. W3 stays out (separate
  discussion).
