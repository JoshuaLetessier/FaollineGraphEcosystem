# Implementation Plan: ReactiveEvaluator re-lock event + reactive-hosting doc clarity (slice 7)

**Branch**: `026-reactive-relock-event` | **Date**: 2026-06-11 | **Spec**: [spec.md](spec.md)

## Summary

One additive public event on `ReactiveEvaluator` — `OnNodeLocked` (`Action<string>`), the Locked-state
counterpart of `OnNodeAvailable`/`OnNodeCompleted` — emitted from the existing `EmitFor` so it fires on
backward transitions in `Reevaluate` and during the initial emission in `Start()`, with no change to derivation
or the existing events. Plus a README restructure of the reactive-hosting section: lead with `MarkCompleted`,
state the `MarkCompleted` **or** `OnCollectionChanged → Reevaluate` (not both) caveat, and document the new
event. graphcore + gameflow untouched; graphstandard `0.5.0 → 0.6.0`; suites stay green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: graphcore (`BaseGraph`, `BaseContext`,
`ReactiveNodeState`). **Storage**: none. **Testing**: NUnit EditMode — Locked→Available→Locked drives
`OnNodeLocked` on the backward transition; initial emission fires it for initially-Locked nodes; no fire when a
node's state is unchanged. **Constraints**: graphcore + gameflow untouched; graphstandard append-only (one new
event; existing members unchanged); `[GraphStandard]`; XML docs. **Scope**: edit `ReactiveEvaluator.cs` +
EditMode tests + README/CHANGELOG + package bump.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | graphcore + gameflow untouched. graphstandard additive `0.5.0 → 0.6.0`; only a new event + emission branch. Existing suites green. |
| II. Universal Abstractions Only | ✅ PASS | Event over generic node ids; no domain vocabulary. |
| III. Specification-First | ✅ PASS | spec approved (16/16). |
| IV. Test-Driven Development | ✅ PASS | Test-first: re-lock transition, initial emission, no-fire-on-unchanged. |
| V. Simplicity (YAGNI) | ✅ PASS | One event reusing `EmitFor`; no new state or config. |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context shape change. |
| VII. Cross-lib via SubGraph only | ✅ PASS | graphstandard depends only on graphcore. |
| Dev standards | ✅ PASS | XML docs on the event; one class per file (edited). |

**Result**: PASS — no violations.

## Project Structure

```text
com.faolline.graphstandard/
├── package.json                                   # 0.5.0 → 0.6.0
├── README.md                                      # reactive-hosting section restructured (W1) + OnNodeLocked
├── CHANGELOG.md                                   # 0.6.0
├── Runtime/Reactive/ReactiveEvaluator.cs          # MODIFIED (additive): OnNodeLocked + EmitFor branch
└── Tests/EditMode/Reactive/ReactiveRelockEventTests.cs   # NEW

# com.faolline.graphcore/ and com.faolline.graphgameflow/ : UNCHANGED.
```

## Phase 0 — Research

See [research.md](research.md): R1 emit from the existing `EmitFor` (one branch — guarantees identical
transition/initial-emission semantics as the other two events); R2 initial-emission symmetry is intended; R3 doc
leads with `MarkCompleted`, bridge is the alternative.

## Phase 1 — Design & Contracts

[data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
[quickstart.md](quickstart.md).

## Implementation Sequencing (TDD)

1. **Tests (test-first)** `Tests/EditMode/Reactive/ReactiveRelockEventTests.cs`: (a) k-of-N node, record to
   Available then `RemoveFromCollection` + `Reevaluate` → `OnNodeLocked` fires once for it, state Locked;
   (b) `Start()` fires `OnNodeLocked` for an initially-Locked node, not for an Available/Completed one;
   (c) a `Reevaluate` leaving a node Available raises no `OnNodeLocked` for it. Confirm RED (event missing).
2. **Implement**: add `public event Action<string> OnNodeLocked;` (XML docs) and an
   `else if (state == ReactiveNodeState.Locked) OnNodeLocked?.Invoke(nodeId);` branch in `EmitFor`. Confirm GREEN.
3. **Finalize**: full suite via batchmode (graphstandard + graphcore + gameflow EditMode green, PlayMode 9
   green); bump `0.6.0`; README (W1 restructure + event) + CHANGELOG; verify append-only.

## Complexity Tracking

> No violations — empty.
