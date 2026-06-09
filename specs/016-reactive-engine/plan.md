# Implementation Plan: P3 — Reactive engine (ReactiveEvaluator)

**Branch**: `016-reactive-engine` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-reactive-engine/spec.md`

## Summary

A new buffer library **`com.faolline.graphstandard` (0.1.0)** is created above graphcore, hosting the first
**non-linear execution engine**: a cursor-less **`ReactiveEvaluator`**. It reads the graphcore substrate
(graph + edges as **prerequisites**: an edge A→C means "C requires A") and derives each node's
**`ReactiveNodeState`** — Locked / Available / Completed — from graph topology + a **completed-set**
(a graphcore P2 string-set collection on the shared context). A node is Completed if its id is in the
set; Available if it has no prerequisites or all are Completed (**AND**); else Locked. `MarkCompleted`
adds the id to the collection and re-evaluates, **cascading** unlocks and emitting `OnNodeAvailable` /
`OnNodeCompleted`. Because completion is the P2 collection, it **persists** (save) and **history-restores**
(step-back); re-evaluation is **idempotent and reversible** — "back" is a re-pass, not undo. graphcore is
**untouched**. MVP = AND prerequisites + host-driven completion; threshold/OR (P4), Flow, time are out.

## Technical Context

**Language/Version**: C# / Unity 6000.0.

**Primary Dependencies**: `com.faolline.graphcore` (0.5.0) — consumed via its public substrate (BaseGraph,
BaseNodeData, BaseEdgeData, BaseContext collections). No other dependency. graphstandard is a NEW package.

**Storage**: the completed-set is a graphcore P2 collection on the shared `BaseContext` — durable
(save + history) for free; the evaluator holds only a derived in-memory state cache (for transition
detection), rebuilt by re-evaluation.

**Testing**: Unity Test Framework, **EditMode only** (headless). New `com.faolline.graphstandard.Tests.EditMode`
assembly. Run via batchmode (editor closed; `-runTests -testPlatform EditMode` WITHOUT `-quit`; re-run once
after source changes; verify the XML — see memory). The existing 586-test graphcore+graphTest suite stays
green.

**Target Platform**: Any Unity runtime; Editor 6000.0+.

**Project Type**: New ecosystem library (the buffer lib) + its first engine. graphcore unchanged.

**Performance Goals**: Re-evaluation is O(nodes + edges) per change — derive each node's state by checking
its prerequisites against the completed-set (a HashSet membership test). No per-frame work; the host
triggers re-evaluation on completion/restore.

**Constraints**: graphcore Runtime is NOT modified (SC-005). Universal abstractions only (no domain
vocabulary). EditMode TDD. `[GraphStandard]` log prefix; one class per file; XML docs on new public API.
New lib at 0.1.0 (no graphcore semver bump). asmdef references graphcore by name; Unity generates `.meta`
GUIDs on import.

**Scale/Scope**: One new package (package.json + 2 asmdefs), one enum, one engine class (~6 public members),
four EditMode test files. No graphcore change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore is **untouched** — the engine is built entirely on graphcore's public 0.5.0 surface. New code lives in a separate lib; nothing in the foundation changes (SC-005). The 586-test suite stays green by construction. |
| II. Universal Abstractions Only | ✅ PASS | The engine encodes only universal reactive-DAG semantics (prerequisite satisfaction, availability, completion). Neutral naming (`ReactiveEvaluator`, `MarkCompleted`, `Available`); zero domain words ("puzzle"/"quest"/"region" belong to the future questsystem lib). |
| III. Specification-First | ✅ PASS | `spec.md` approved (checklist all-green) before this plan. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Red-Green-Refactor: failing EditMode tests first for state derivation, cascade, events, durability/re-pass. (C#/Unity tests compile against the new API → gate is GREEN + non-regression via batchmode.) |
| V. Simplicity (YAGNI) | ✅ PASS | AND-of-incoming-edges only (no threshold/OR); host-driven completion (no condition engine); explicit re-evaluation (no auto-subscription required); state derived from the set (no separate persisted node-state). Each is the simplest thing that satisfies the evidenced progression-DAG need. |
| VI. Typed Context Contract | ✅ PASS (N/A to core) | The engine adds NO context state of its own beyond the P2 collection it reads/writes; no new `BaseContext` subclass or clone logic. Completion durability rides P2's existing DeepClone. |
| VII. Cross-lib via SubGraph only | ✅ PASS | graphstandard depends on graphcore via the normal package dependency (a lib above core), not a cross-graph hack; it introduces no new cross-lib coupling mechanism. |
| Dev: no MonoBehaviour/UnityEvent; log prefix; one class per file; XML docs | ✅ PASS | Pure C# (`Action<string>` events); `[GraphStandard]` prefix; `ReactiveNodeState` and `ReactiveEvaluator` in their own files; XML docs on new public API. |

**Result**: PASS — no violations. Creating a new lib is the planned ecosystem evolution (the buffer lib),
not a complexity violation.

## Project Structure

### Documentation (this feature)

```text
specs/016-reactive-engine/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1..R6
├── data-model.md        # Phase 1 — enum, engine, derivation/transition rules
├── quickstart.md        # Phase 1 — host + author walkthrough
├── contracts/
│   └── public-api.md    # Phase 1 — authoritative new public surface + invariants
└── checklists/
    └── requirements.md  # from /speckit-specify (all green)
```

### Source Code (repository root)

```text
com.faolline.graphstandard/                        # NEW buffer lib — version 0.1.0
├── package.json                                   # depends on com.faolline.graphcore
├── Runtime/
│   ├── com.faolline.graphstandard.Runtime.asmdef  # references com.faolline.graphcore.Runtime (by name)
│   └── Reactive/
│       ├── ReactiveNodeState.cs                   # enum: Locked / Available / Completed
│       └── ReactiveEvaluator.cs                   # the cursor-less engine
└── Tests/EditMode/
    ├── com.faolline.graphstandard.Tests.EditMode.asmdef  # refs graphstandard.Runtime + graphcore.Runtime + TestRunner
    └── Reactive/
        ├── ReactiveStateDerivationTests.cs        # US1: Locked/Available/Completed derivation, AND prereqs
        ├── ReactiveCascadeTests.cs                # US2: MarkCompleted cascades unlocks; idempotent re-mark
        ├── ReactiveEventTests.cs                  # US3: OnNodeAvailable/OnNodeCompleted, init emission, no spurious
        └── ReactiveProgressionDagTests.cs         # US4 + SC-006: durable/reversible re-pass; game-like A,B→C→… DAG

# graphcore/ : UNCHANGED (no edits this feature)
```

**Structure Decision**: A new standalone package `com.faolline.graphstandard` is created (the buffer lib
from the roadmap), depending on graphcore. The engine and its state enum are pure C# over graphcore's
public API. asmdefs reference graphcore by assembly name; Unity assigns `.meta` GUIDs on first import
(the batchmode run). No `.meta` is hand-authored. graphcore receives zero edits.

## Phase 0 — Research

See [research.md](research.md): **R1** new lib vs. putting the engine in graphcore/graphTest; **R2** edges
as prerequisites (incoming-edge sources) + AND semantics; **R3** completion via the P2 completed-set
(durability for free) vs. an engine-private state store; **R4** transition detection via a cached state
map + event emission rules; **R5** explicit re-evaluation (MVP) vs. auto-subscribing to the P2 collection
change; **R6** asmdef/package wiring (name references, Unity-generated GUIDs) and the `[GraphStandard]`
conventions.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): `ReactiveNodeState` enum; `ReactiveEvaluator` fields/methods; the
  derivation rule, transition/event rules, and idempotency/reversibility invariants.
- [contracts/public-api.md](contracts/public-api.md): authoritative public surface + testable invariants
  and acceptance→invariant traceability.
- [quickstart.md](quickstart.md): how a host initializes the evaluator over a DAG, marks nodes complete,
  subscribes to unlock events, and re-evaluates after a step-back.

## Implementation Sequencing (TDD, by user-story priority)

1. **Setup — graphstandard package**: create `package.json`, `Runtime` asmdef (→ graphcore.Runtime),
   `Tests/EditMode` asmdef; confirm it compiles empty (batchmode).
2. **US1 — State derivation** (P1): failing `ReactiveStateDerivationTests` → `ReactiveNodeState`,
   `ReactiveEvaluator` ctor/Initialize, prerequisites map (incoming-edge sources), derive
   Completed/Available/Locked, `GetState`, `AvailableNodeIds`, `CompletedNodeIds`.
3. **US2 — Cascade** (P1): failing `ReactiveCascadeTests` → `MarkCompleted` (add to completed-set +
   re-evaluate), unlock cascade, idempotent re-mark.
4. **US3 — Events** (P2): failing `ReactiveEventTests` → `OnNodeAvailable`/`OnNodeCompleted`, init
   emission for initially-available/completed nodes, transition-only firing (no spurious, idempotent).
5. **US4 — Durable / reversible** (P2 / SC-006): failing `ReactiveProgressionDagTests` → public
   `Reevaluate()`; restore a smaller completed-set (DeepClone/CopyValuesFrom round-trip) and confirm the
   smaller satisfied set with no side-effects; a game-like multi-tier DAG scenario.
6. **Finalize**: XML docs on all public API; batchmode = full suite (586 + new) green; note graphstandard
   0.1.0 and graphcore untouched.

## Complexity Tracking

> No constitution violations — section intentionally empty.
