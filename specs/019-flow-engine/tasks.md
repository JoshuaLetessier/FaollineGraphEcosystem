---
description: "Task list for 019-flow-engine (multi-active FlowRunner in graphstandard)"
---

# Tasks: Flow engine (multi-active fork/join, re-pass, one-shot)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD). EditMode only; batchmode (no `-quit`; re-run after source change; verify XML).
Branch `019-flow-engine` (P1–P5 included). graphcore UNTOUCHED.

## Phase 1: US1 + US2 — Fork + Join (Priority: P1) 🎯 MVP

- [X] T001 [P] [US1] Write `FlowForkJoinTests` in `com.faolline.graphstandard/Tests/EditMode/Flow/FlowForkJoinTests.cs`: firing a node with 3 unconditional successors fires all 3 (INV-1); a false edge condition delivers no token, others still fire (INV-4); a chain A→B→C all fire (INV-1); a 2-incoming AND-join fires only after both predecessors fire, exactly once (INV-2); a fork reconverging at a join fires the join once (INV-2/INV-5); a join configured with threshold 1 fires on the first arrival (INV-2); `HasFired`/`FiredNodeIds` reflect fires (INV-10). Confirm RED.
- [X] T002 [US1] Create `com.faolline.graphstandard/Runtime/Flow/FlowRunner.cs`: ctor `(BaseGraph, BaseContext, IReadOnlyCollection<string> oneShotNodeIds=null, IReadOnlyDictionary<string,int> joinThresholds=null, int maxFiresPerPropagation=10000)` (copy config; precompute incoming counts + outgoing index); `OnNodeFired`; `Fire`; `Reset`; `HasFired`; `FiredNodeIds`; the cascade per data-model (run OnEnterActions, emit, clear arrived, fork over condition-passing edges, deliver edge-id tokens, fire target at threshold). `[GraphStandard]` prefix; XML docs. Confirm T001 GREEN.

## Phase 2: US3 — Re-pass + one-shot (Priority: P2)

- [X] T003 [P] [US3] Write `FlowRePassOneShotTests` in `com.faolline.graphstandard/Tests/EditMode/Flow/FlowRePassOneShotTests.cs`: a non-one-shot node fires on each `Fire` (INV-5); a one-shot node fires once across two `Fire`s, then again after `Reset` (INV-6/INV-10); a cyclic graph A→B→A halts at the fire cap with a `[GraphStandard]` warning (use `LogAssert.Expect`) instead of hanging (INV-7). Confirm RED then GREEN (covered by T002; fix only if a gap appears).

## Phase 3: US4 — Actions + ability scenario (Priority: P2)

- [X] T004 [P] [US4] Write `FlowAbilityScenarioTests` in `com.faolline.graphstandard/Tests/EditMode/Flow/FlowAbilityScenarioTests.cs`: a node whose enter-action adds an id to a collection ⇒ after firing the context contains it (INV-3); a conditional edge gates propagation (INV-4); a cast→{damage,debuff,vfx}→cooldown flow fired once ⇒ all five fire, cooldown last (SC-006). Use a small in-test BaseAction/BaseCondition or reuse graphTest-style nodes via the context API directly. Confirm RED then GREEN.

## Phase 4: Back-compat + Finalize

- [X] T005 Run the ENTIRE existing 621-test suite UNCHANGED via batchmode; confirm green (graphcore untouched, SC-007). Record totals.
- [X] T006 Bump `com.faolline.graphstandard/package.json` `0.2.0` → `0.3.0`.
- [X] T007 [P] Verify XML docs on the FlowRunner public API; `[GraphStandard]` prefix; validate quickstart; full batchmode green (621 + new Flow tests).

## Dependencies

- US1/US2 (T001→T002) → core. US3 (T003), US4 (T004) → after T002. Back-compat/Finalize (T005-T007) last.

## Notes

- Only the new `com.faolline.graphstandard/Runtime/Flow/` + `Tests/EditMode/Flow/` + `package.json` change.
  graphcore + ReactiveEvaluator UNTOUCHED.
- Thresholds/one-shot are ctor config (no graphcore field). Cycles bounded by `maxFiresPerPropagation`.
- For test actions/conditions: a minimal nested test `BaseAction`/`BaseCondition` (plain C# subclass) keeps
  the test self-contained; firing calls `action.Execute(context)` and edges call `condition.Evaluate(context)`.
