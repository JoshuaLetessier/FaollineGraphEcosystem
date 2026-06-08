# Implementation Plan: P1 — Signals (host→runtime event injection)

**Branch**: `014-signals` | **Date**: 2026-06-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-signals/spec.md`

## Summary

GraphCore gains a **signal** capability: the host can raise a named, optionally-payloaded transient
event into a running graph, graph logic can subscribe to and read the last payload of a named signal,
and a node can be flagged to **hold execution until a named signal arrives** (the push→pull bridge over
the existing pull-based runner). Signals live as a **separate, dedicated channel on `BaseContext`** —
NOT overloaded onto the typed-parameter store / `OnParameterChanged` — so a signal stays a *transient
event*, never *stored state* (it is excluded from `GetAllParameters()`, `DeepClone`, and save). The
await capability is a single **append-only `string AwaitSignalName` field on `BaseNodeData`** (empty ⇒
not awaiting), so any node can await and pre-existing assets are byte-for-byte unchanged. The runner
enters a new append-only **`RunnerState.WaitingForSignal`** instead of raising `OnNodeCompleted`, and
`BaseRunner.RaiseSignal(...)` delivers the event then, if the current node awaits that name, advances
using the existing edge-selection rules. Step-back re-arms the wait automatically (re-entering an
awaiting node re-detects its `AwaitSignalName`). All additions are append-only → graphcore **0.3.0 →
0.4.0 (semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0 (`com.faolline.graphcore` `unity: 6000.0`).

**Primary Dependencies**: none new. Changes are confined to the existing graphcore Runtime assembly
(`com.faolline.graphcore.Runtime`). No editor, no external packages.

**Storage**: N/A at the core layer. Signals are **transient** and deliberately excluded from
`GetAllParameters()` (the save surface) and from history `DeepClone`/`CopyValuesFrom`. On-disk
persistence remains a downstream concern, unaffected.

**Testing**: Unity Test Framework, **EditMode only** (the runner/context are headless — Principle IV).
Run via Unity 6000.3 batchmode (editor closed; delete `Temp/UnityLockfile` if stale) or Coplay
`run_tests`. The capability is additionally exercised/stress-tested in `com.faolline.graphTest`.

**Target Platform**: Any Unity runtime; Editor 6000.0+.

**Project Type**: Foundation library evolution — modifies graphcore core (`BaseContext`, `BaseNodeData`,
`RunnerState`, `BaseRunner`) and adds one new public type (`SignalArgs`). No new package, no new assembly.

**Performance Goals**: Zero added cost on the existing path. A node with an empty `AwaitSignalName`
takes one extra `string.IsNullOrEmpty` check per entry. Raising a signal is one dictionary lookup +
a subscriber-list iteration. No per-frame work; no allocation when unused.

**Constraints**: Foundation Stability (Principle I) is NON-NEGOTIABLE — every change MUST be append-only
and semver-MINOR; the **entire existing graphcore EditMode suite MUST stay green unmodified** as the
proof of non-breakage (SC-002). No `MonoBehaviour`/`UnityEvent` in Runtime; `[GraphCore]` log prefix;
one class per file; XML docs on new public API. `INodeExecutor` is frozen (untouched). Existing public
signatures on `BaseContext`/`BaseRunner`/`BaseNodeData`/`RunnerState` are unchanged (only additions).

**Scale/Scope**: Small/surgical — one new `string` field on `BaseNodeData`, one new `RunnerState` value,
one new public struct (`SignalArgs`), a signal channel on `BaseContext` (~4 public methods + 2 private
fields), one new event + two `RaiseSignal` overloads + one await branch on `BaseRunner`. New EditMode
test files in graphcore + exercising scenarios in graphTest.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | All additions append-only: new methods on `BaseContext`; new `string AwaitSignalName` on `BaseNodeData` (default empty ⇒ pre-existing assets unchanged); new `RunnerState.WaitingForSignal = 4` (appended, no reorder/removal); new `BaseRunner.RaiseSignal` overloads + `OnWaitingForSignal` event + one guarded branch. No public signature removed/changed. Non-awaiting path identical to 0.3.0. Version 0.3.0 → 0.4.0. Existing suite green = the gate. |
| II. Universal Abstractions Only | ✅ PASS | "An external event arriving at a graph" is universal to graph systems. Neutral naming (`Signal`, `RaiseSignal`, `AwaitSignalName`); zero domain vocabulary (no "click", "item", "puzzle"). |
| III. Specification-First | ✅ PASS | `spec.md` approved (requirements checklist all-green, no open markers) before this plan. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Red-Green-Refactor: failing EditMode tests written first for pub/sub delivery, no-op-on-no-subscriber, broadcast, no-payload distinction, await/resume, name-mismatch, payload read, back-compat, step-back re-arm; confirmed failing before implementation. |
| V. Simplicity (YAGNI) | ✅ PASS | Reuses the proven subscriber pattern (snapshot-on-fire) rather than new infra; await is a **field on `BaseNodeData`**, not a new node type + executor + editor surface; transient-only (no latching). Separate signal channel chosen over overloading `OnParameterChanged` BECAUSE overloading would persist payloads into the parameter store (save/history pollution) and cannot express "no payload" — that is added complexity, not less. |
| VI. Typed Context Contract | ✅ PASS | Signals are deliberately **outside** the typed-parameter store: not in `_params`, so `GetAllParameters()`/`DeepClone`/`CopyValuesFrom` are unaffected and subclasses keep overriding only `CreateCloneInstance`. No raw parameter-key literals introduced. |
| VII. Cross-lib via SubGraph only | ✅ PASS | Signals are a host↔runtime mechanism within a running instance; no new cross-graph/cross-lib coupling. |
| Dev: no MonoBehaviour/UnityEvent in Runtime; `[GraphCore]` prefix; one class per file; XML docs | ✅ PASS | Pure C# (`Action<SignalArgs>`); new public API gets XML docs; `SignalArgs` in its own file; misuse warnings use `[GraphCore]`. |

**Result**: PASS — no violations, no Complexity Tracking entries required. The single sensitive points
(modifying foundation classes) are mitigated by append-only design + the unmodified-suite-green gate.

## Project Structure

### Documentation (this feature)

```text
specs/014-signals/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1..R6
├── data-model.md        # Phase 1 — type changes, invariants, state transitions
├── quickstart.md        # Phase 1 — author + integrator walkthrough
├── contracts/
│   └── public-api.md    # Phase 1 — authoritative new public surface + invariants
└── checklists/
    └── requirements.md  # from /speckit-specify (all green)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── package.json                                  # version 0.3.0 → 0.4.0 (MINOR)
├── Runtime/
│   ├── Graph/
│   │   └── BaseContext.cs                         # + signal channel: _signalSubs, _lastSignals;
│   │                                              #   RaiseSignal / RaiseSignal<T> / OnSignal / OffSignal /
│   │                                              #   TryGetLastSignal. NOT touched: _params, DeepClone,
│   │                                              #   GetAllParameters, CopyValuesFrom (signals excluded).
│   ├── Signals/
│   │   └── SignalArgs.cs                          # NEW public readonly struct: Name, HasPayload,
│   │                                              #   GetPayload<T>(), PayloadBoxed
│   ├── Nodes/
│   │   └── BaseNodeData.cs                        # + string AwaitSignalName (append-only, default "")
│   └── Execution/
│       ├── RunnerState.cs                         # + WaitingForSignal = 4 (appended)
│       └── BaseRunner.cs                          # EnterCurrentNode: await branch (state=WaitingForSignal
│                                                  #   + OnWaitingForSignal, skip OnNodeCompleted);
│                                                  #   RaiseSignal / RaiseSignal<T> (deliver then resume);
│                                                  #   set _state=NodeReady authoritatively before OnNodeCompleted
└── Tests/EditMode/
    ├── Signals/
    │   ├── SignalChannelTests.cs                  # pub/sub delivery, broadcast, no-op-no-subscriber,
    │   │                                          #   no-payload distinction, re-entrant raise (US1)
    │   └── SignalPayloadReadTests.cs              # TryGetLastSignal; condition reads last payload (US3)
    └── Execution/
        ├── AwaitSignalRunnerTests.cs              # hold-on-await, resume-on-match, name-mismatch keeps
        │                                          #   waiting, edge-selection on resume (US2)
        ├── SignalBackCompatTests.cs               # no awaits + no signals ⇒ identical; existing suite green
        └── SignalHistoryTests.cs                  # step-back into an awaiting node re-arms its wait (FR-012)

com.faolline.graphTest/                            # exercising/stress per FR-013 (sandbox)
└── (Runtime + Tests)                              # a RaiseSignal demo condition/action + an await node
                                                   #   scenario; details deferred to tasks.md
```

**Structure Decision**: No new package or assembly — surgical evolution of graphcore's Runtime, plus one
new public type (`SignalArgs`) in a `Runtime/Signals/` folder (one class per file). The signal channel is
added to `BaseContext` (so any context subtype inherits it and the runner needs no type checks); the
await flag is added to `BaseNodeData` (so any node can await, consistent with how lifecycle metadata
already lives there). graphTest receives the FR-013 exercising in a later phase (tasks).

## Phase 0 — Research

See [research.md](research.md): **R1** separate signal channel vs. overloading `OnParameterChanged`
(decision: separate channel); **R2** await marker as a `BaseNodeData` field vs. a dedicated node type
(decision: field); **R3** `RunnerState.WaitingForSignal` + `OnWaitingForSignal` event, append-only enum
safety, and authoritative state-setting in `EnterCurrentNode`; **R4** payload model (`SignalArgs`
readonly struct, scalar-only, "no payload" distinct); **R5** history/step-back (wait re-arms on
re-entry; signals excluded from snapshots); **R6** transient (edge-triggered) delivery and why latching
is deferred to the Reactive engine (roadmap P3).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): exact field/method changes to `BaseContext`, `BaseNodeData`,
  `RunnerState`, `BaseRunner`; the `SignalArgs` struct; delivery, await/resume, and history invariants;
  the runner state-transition table.
- [contracts/public-api.md](contracts/public-api.md): authoritative new public surface + testable
  invariants (delivery table, await/resume lockstep, back-compat, step-back re-arm).
- [quickstart.md](quickstart.md): how an author flags an awaiting node and how an integrator subscribes,
  raises (via the runner to resume, or via the context for pure pub/sub), and reads payloads.

## Implementation Sequencing (TDD, by user-story priority)

1. **US1 — Signal channel on `BaseContext`** (P1 storage/delivery layer): failing `SignalChannelTests`
   first → implement `SignalArgs`, `_signalSubs`/`_lastSignals`, `RaiseSignal`/`RaiseSignal<T>`
   (validate scalar type like `Set<T>`), `OnSignal`/`OffSignal` (snapshot-on-fire), broadcast,
   no-op-on-no-subscriber, no-payload distinction.
2. **US2 — Await/resume in `BaseRunner`** (P1 wiring): failing `AwaitSignalRunnerTests` → add
   `BaseNodeData.AwaitSignalName`, `RunnerState.WaitingForSignal`, the await branch in `EnterCurrentNode`
   (state + `OnWaitingForSignal`, skip `OnNodeCompleted`; set `NodeReady` authoritatively on the normal
   path), `BaseRunner.RaiseSignal` overloads (deliver via context, then `ExitAndAdvance` when the current
   node awaits the raised name), name-mismatch keeps waiting, resume uses existing edge selection.
3. **US3 — Payload read by graph logic** (P2): failing `SignalPayloadReadTests` → `TryGetLastSignal`,
   and a graphTest condition that branches on a read payload.
4. **Back-compat lock** (FR-008): `SignalBackCompatTests` → assert no-await/no-signal path untouched,
   then run the **entire pre-existing suite unmodified** (must be green).
5. **History/step-back** (FR-012): failing `SignalHistoryTests` → confirm re-entering an awaiting node
   re-arms the wait; confirm signals are excluded from `DeepClone`/`CopyValuesFrom`.
6. **graphTest exercising** (FR-013): a demo await-node scenario + a RaiseSignal-driven advance + a
   payload-reading condition in the sandbox.
7. **Finalize**: bump `package.json` to 0.4.0; XML docs on all new public API; batchmode run = full suite
   green; semver assessment note.

## Complexity Tracking

> No constitution violations — section intentionally empty.
