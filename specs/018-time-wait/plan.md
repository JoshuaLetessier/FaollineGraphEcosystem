# Implementation Plan: P5 — Time (host-fed wait / timeout)

**Branch**: `018-time-wait` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: `specs/018-time-wait/spec.md`

## Summary

The Linear runner gains **time**, mirroring P1's await-signal: a node may declare an append-only
**`WaitDuration`** (seconds); entering it makes `BaseRunner` hold in a new **`RunnerState.WaitingForTime`**
(firing `OnWaitingForTime`) instead of raising `OnNodeCompleted`. The host advances the held node by
calling **`BaseRunner.Tick(float deltaSeconds)`**; when the accumulated fed time reaches the duration, the
runner advances using the existing edge rules. The runner **owns no clock** — pause is not ticking, slow-mo
is a scaled dt, fast-forward is a large dt. `WaitDuration = 0` ⇒ no hold (identical to today). Await-signal
takes precedence when both are set on one node. Step-back re-arms the countdown. All additions append-only
→ graphcore **0.5.0 → 0.6.0 (semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0. **Dependencies**: none new (graphcore Runtime only).
**Storage**: N/A — the remaining countdown is transient runner state (re-armed on re-entry; not persisted in
the MVP). **Testing**: Unity Test Framework, EditMode only, headless; batchmode (no `-quit`; re-run after
source change; verify XML). **Project Type**: foundation evolution of the Linear runner. **Performance**:
zero added cost when unused (one `WaitDuration > 0` check per entry; one subtraction per tick while waiting).
**Constraints**: Foundation Stability NON-NEGOTIABLE — append-only, semver MINOR; the entire existing
612-test suite stays green unmodified (no wait ⇒ 0.5.0). No `MonoBehaviour`/`UnityEvent`; `[GraphCore]`
prefix; one class per file; XML docs. **Scope**: one `float` field on `BaseNodeData`, one `RunnerState`
value, one runner field + `Tick` method + `OnWaitingForTime` event + one hold branch in `EnterCurrentNode`.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | All append-only: `WaitDuration` (default 0 ⇒ existing assets unchanged), `RunnerState.WaitingForTime = 5` (appended), new `Tick`/`OnWaitingForTime`, one guarded branch. No signature removed/changed. No-wait path identical to 0.5.0. 0.5.0 → 0.6.0. |
| II. Universal Abstractions Only | ✅ PASS | "Hold for a duration" is universal; neutral naming (`WaitDuration`, `Tick`), zero domain vocabulary. |
| III. Specification-First | ✅ PASS | spec.md approved (checklist green). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Failing EditMode tests first: hold, tick-to-advance, overshoot, pause(Tick 0), default-no-wait, signal-precedence, inert Proceed, step-back re-arm. |
| V. Simplicity (YAGNI) | ✅ PASS | Mirrors the proven P1 hold/resume shape; host-fed time (no internal clock, so pause/slow-mo are free); re-arm-on-re-entry (no partial-countdown persistence). |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context state added; the countdown is runner-local. |
| VII. Cross-lib via SubGraph only | ✅ PASS | No new cross-lib mechanism. |
| Dev standards | ✅ PASS | Pure C# (`Action<BaseNodeData,float>`); `[GraphCore]` prefix; XML docs; one class per file. |

**Result**: PASS — no violations.

## Project Structure

```text
specs/018-time-wait/{plan,research,data-model,quickstart}.md, contracts/public-api.md, checklists/requirements.md

com.faolline.graphcore/
├── package.json                       # 0.5.0 → 0.6.0
├── Runtime/
│   ├── Nodes/BaseNodeData.cs          # + float WaitDuration (append-only, default 0)
│   └── Execution/
│       ├── RunnerState.cs             # + WaitingForTime = 5 (appended)
│       └── BaseRunner.cs              # + _waitRemaining; Tick(float); OnWaitingForTime; hold branch in
│                                      #   EnterCurrentNode (after the await-signal branch)
└── Tests/EditMode/Execution/
    └── TimeWaitRunnerTests.cs         # hold/tick/overshoot/pause/default/signal-precedence/inert/step-back
```

**Structure Decision**: graphcore Linear-runner evolution, structurally identical to P1 (await-signal). The
time wait is a second hold mechanism layered after the signal branch; everything else is unchanged.

## Phase 0 — Research

See [research.md](research.md): R1 host-fed `Tick` vs. an internal clock; R2 `WaitDuration` on `BaseNodeData`
vs. a dedicated node type; R3 await-signal precedence + re-arm-on-re-entry; R4 the `WaitingForTime` state +
inert `Proceed`.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD)

1. Failing `TimeWaitRunnerTests` → add `BaseNodeData.WaitDuration`, `RunnerState.WaitingForTime`, the hold
   branch in `EnterCurrentNode`, `_waitRemaining`, `Tick`, `OnWaitingForTime`.
2. Back-compat: no-wait path identical; run the full 612-suite unmodified (green).
3. Finalize: bump 0.6.0; XML docs; batchmode green.

## Complexity Tracking

> No violations — empty.
