# Implementation Plan: Flow engine (multi-active fork/join, re-pass, one-shot)

**Branch**: `019-flow-engine` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: `specs/019-flow-engine/spec.md`

## Summary

A new **`FlowRunner`** in `com.faolline.graphstandard` — the third execution engine, multi-active and
cursor-less. Firing a node runs its enter-actions over the shared context, emits `OnNodeFired`, then
**forks**: it delivers a token along **every** outgoing edge whose condition passes. A target node holds
its arrived tokens and **fires on a join** when the count reaches its threshold (default = its incoming-edge
count = AND-rendezvous; per-node configurable for k-of-N / OR). Propagation is a synchronous cascade
resolving the reachable sub-flow in one `Fire`. **Re-pass** is intentional (cycles permitted, bounded by a
fire-count safety cap that warns); a per-node **one-shot** set fires a node at most once until `Reset`.
Join thresholds and one-shot are FlowRunner **configuration** (not graphcore fields), so **graphcore is
untouched**. graphstandard **0.2.0 → 0.3.0 (semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0. **Dependencies**: `com.faolline.graphcore` 0.6.0 (public substrate:
graph, edges, conditions, `OnEnterActions`). **Storage**: in-memory propagation state (fired set, arrived
tokens); no persistence. **Testing**: EditMode only, headless; batchmode (no `-quit`; re-run; verify XML);
the existing 621-test suite stays green; graphstandard adds its own. **Project Type**: second engine in the
buffer lib. **Performance**: a fire touches a node's outgoing edges; the whole cascade is O(fired·out-degree),
bounded by the safety cap. **Constraints**: graphcore untouched; universal abstractions; `[GraphStandard]`
prefix; one class per file; XML docs; EditMode TDD. **Scope**: one new `FlowRunner` class + tests; package
bump.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore untouched; engine is new code in graphstandard. The 621-test suite stays green by construction. graphstandard 0.2.0 → 0.3.0. |
| II. Universal Abstractions Only | ✅ PASS | Fork / join / re-pass / one-shot are universal flow semantics. Neutral naming (`FlowRunner`, `Fire`, `OnNodeFired`); no domain vocabulary. |
| III. Specification-First | ✅ PASS | spec.md approved. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Failing EditMode tests first for fork, conditional fork, chain, AND-join, k-of-N join, re-pass, one-shot, cycle-bounded, action-runs, ability scenario. |
| V. Simplicity (YAGNI) | ✅ PASS | Synchronous cascade (no scheduler); thresholds/one-shot as config (no graphcore field, no editor); reuses graphcore enter-actions + edge conditions; cycle safety via a fire cap (no cycle analysis). |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context state added; flow nodes mutate context via their existing enter-actions. |
| VII. Cross-lib via SubGraph only | ✅ PASS | In-lib engine; no new cross-lib mechanism. |
| Dev standards | ✅ PASS | Pure C# (`Action<string>`); `[GraphStandard]` prefix; XML docs; one class per file. |

**Result**: PASS — no violations.

## Project Structure

```text
specs/019-flow-engine/{plan,research,data-model,quickstart}.md, contracts/public-api.md, checklists/requirements.md

com.faolline.graphstandard/
├── package.json                                   # 0.2.0 → 0.3.0
├── Runtime/Flow/
│   └── FlowRunner.cs                              # multi-active token-propagation engine
└── Tests/EditMode/Flow/
    ├── FlowForkJoinTests.cs                       # fork (activate-all), conditional fork, chain, AND-join, k-of-N/OR join
    ├── FlowRePassOneShotTests.cs                  # re-fire, one-shot, Reset, cycle bounded by the cap
    └── FlowAbilityScenarioTests.cs                # cast → fork → effects → join → cooldown; action mutates context

# graphcore/ : UNCHANGED.  ReactiveEvaluator/ReactiveNodeState : UNCHANGED.
```

**Structure Decision**: `FlowRunner` is pure C# over graphcore's public API, beside `ReactiveEvaluator` in
graphstandard. Thresholds and one-shot are constructor configuration (node-id map/set), so graphcore needs
no new field and the engine stays fully in the buffer lib.

## Phase 0 — Research

See [research.md](research.md): R1 token-propagation cascade vs. a step scheduler; R2 join threshold +
one-shot as FlowRunner config vs. graphcore node fields; R3 cycles + the fire-count safety cap; R4 firing
runs `OnEnterActions` + edge conditions gate (reuse, no new node behaviour); R5 arrived-tokens keyed by
edge id, cleared on fire (enables re-pass and correct AND-join).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD)

1. **US1/US2 — fork + join**: failing `FlowForkJoinTests` → `FlowRunner` ctor (incoming-count map, optional
   one-shot set + join-threshold map + fire cap), `Fire`, the cascade (run enter-actions, emit
   `OnNodeFired`, fork over condition-passing edges, deliver tokens, fire target at threshold), `OnNodeFired`,
   `HasFired`/`FiredNodeIds`.
2. **US3 — re-pass + one-shot**: failing `FlowRePassOneShotTests` → one-shot skip, `Reset`, cycle bounded by
   the fire cap + `[GraphStandard]` warning.
3. **US4 — actions + ability scenario**: failing `FlowAbilityScenarioTests` → confirm enter-actions mutate
   the context and a cast→fork→join→cooldown flow resolves once; conditional edge gating.
4. **Back-compat**: run the entire 621-suite unchanged (graphcore untouched) + graphstandard green.
5. **Finalize**: bump 0.3.0; XML docs; batchmode green.

## Complexity Tracking

> No violations — empty.
