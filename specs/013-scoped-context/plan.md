# Implementation Plan: Global & Local Execution Contexts

**Branch**: `013-scoped-context` | **Date**: 2026-06-04 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-scoped-context/spec.md`

## Summary

GraphCore gains a **global + local** execution-context capability so a sub-graph flagged "opens a scope"
runs with a transient **local context** layered over the persistent **global context**: its temporary
writes vanish when it ends, while reads fall through to the global context and writes to global-declared
variables persist. The capability lives as an **optional local overlay inside `BaseContext`** (a nullable
second value bucket), so when no scope is ever opened `BaseContext` behaves byte-for-byte as today. The
runner opens the overlay on entering a scope-opening sub-graph (seeding the local bucket from that
sub-graph's parameters) and discards it when that sub-graph ends, in lockstep with the existing graph
stack. Write routing is **resolve-and-write** (local if the key lives locally, else global if it lives
globally, else local while a scope is open) — so a variable's "declared home" is simply the bucket it
already lives in, requiring **no change to `ParameterData`**. Scopes never nest (author-confirmed), so the
overlay is a single flat level, not a stack. All additions are append-only → graphcore **0.2.0 → 0.3.0
(semver MINOR)**.

## Technical Context

**Language/Version**: C# / Unity 6000.0 (`com.faolline.graphcore` `unity: 6000.0`).

**Primary Dependencies**: none new. Changes are confined to the existing graphcore Runtime assembly
(`com.faolline.graphcore.Runtime`). No editor, no external packages.

**Storage**: N/A at the core layer. History snapshots (in-memory) must capture the overlay; on-disk
persistence remains a downstream concern (gameflow + `com.faolline.savesystem.core`), unaffected — the
save lib serialises key→value sets regardless of bucket count.

**Testing**: Unity Test Framework, **EditMode only** (the runner/context are headless — Principle IV). Run
via Unity 6000.3 batchmode (editor closed; delete `Temp/UnityLockfile` if stale) or Coplay `run_tests`.

**Target Platform**: Any Unity runtime; Editor 6000.0+.

**Project Type**: Foundation library evolution — modifies graphcore core (`BaseContext`, `BaseRunner`,
`SubGraphNodeData`, `GraphExecutionState`). No new package, no new assembly.

**Performance Goals**: Zero added cost on the existing path (no scope open ⇒ no overlay allocation, one
extra null-check per read/write). One dictionary allocation per scope open; freed on scope close. No
per-frame work.

**Constraints**: Foundation Stability (Principle I) is NON-NEGOTIABLE — every change MUST be append-only
and semver-MINOR; the **entire existing graphcore EditMode suite MUST stay green unmodified** as the proof
of non-breakage (SC-004). `[GraphCore]` log prefix; XML docs on new public API; no `MonoBehaviour`/
`UnityEvent` in Runtime; one class per file. `BaseContext.Set/Get` signatures stay frozen (overlay logic
is internal, not a signature change, and not made `virtual`).

**Scale/Scope**: Small/surgical — one new bool field on `SubGraphNodeData`, one new bool field on
`GraphExecutionState`, ~3 new public methods + internal overlay on `BaseContext`, two new branches in
`BaseRunner` (sub-graph enter / end-pop). Four new EditMode test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | All additions append-only: new methods on `BaseContext` (MINOR), new `bool` fields on `SubGraphNodeData`/`GraphExecutionState` (default false ⇒ pre-existing assets unchanged), new internal runner branch. No public signature removed/changed. `Set/Get` behaviour identical whenever no scope is open. Version 0.2.0 → 0.3.0. Existing suite green = the gate. |
| II. Universal Abstractions Only | ✅ PASS | Global-vs-local **value lifetime** is universal to graph systems; neutral naming (`BeginLocalContext`/local context), zero domain vocabulary ("scene" stays in gameflow). |
| III. Specification-First | ✅ PASS | `spec.md` approved (requirements checklist all-green, no open markers) before this plan. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Red-Green-Refactor: failing EditMode tests written first for overlay routing, runner lockstep, isolation-on-exit, durable global write, and step-back; confirmed failing before implementation. |
| V. Simplicity (YAGNI) | ✅ PASS | No-nesting (author-confirmed) ⇒ a single flat overlay, **not** a scope stack. Resolve-and-write routing ⇒ **no `ParameterData` change**, no global-keys registry. Overlay lives in `BaseContext` (no new subclass, no virtual dispatch). |
| VI. Typed Context Contract | ✅ PASS | Overlay is managed by `BaseContext` itself, so `base.DeepClone()` copies it for every subclass automatically; subclasses keep overriding only `CreateCloneInstance`. No raw key literals introduced. Documented in data-model. |
| VII. Cross-lib via SubGraph only | ✅ PASS | Scope opening is driven by a flag on the existing `SubGraphNodeData`; no new cross-graph mechanism. |
| Dev: no MonoBehaviour/UnityEvent in Runtime; `[GraphCore]` prefix; one class per file; XML docs | ✅ PASS | All changes are pure C# in Runtime; new public API gets XML docs; warning on misuse uses `[GraphCore]`. |

**Result**: PASS — no violations, no Complexity Tracking entries required. The single sensitive point
(modifying a foundation class) is mitigated by append-only design + the unmodified-suite-green gate.

## Project Structure

### Documentation (this feature)

```text
specs/013-scoped-context/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1..R8
├── data-model.md        # Phase 1 — type changes & invariants
├── quickstart.md        # Phase 1 — author + integrator walkthrough
├── contracts/
│   └── public-api.md    # Phase 1 — authoritative new public surface + invariants
└── checklists/
    └── requirements.md  # from /speckit-specify (all green)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── package.json                                  # version 0.2.0 → 0.3.0 (MINOR)
├── Runtime/
│   ├── Graph/
│   │   └── BaseContext.cs                         # + local overlay: _local bucket, _localActive;
│   │                                              #   BeginLocalContext / EndLocalContext / HasLocalContext;
│   │                                              #   overlay-aware Set/Get/TryGet/Has/DeepClone/CopyValuesFrom/GetAllParameters
│   ├── Nodes/
│   │   └── SubGraphNodeData.cs                    # + bool OpensScope (append-only, default false)
│   └── Execution/
│       ├── GraphExecutionState.cs                 # + bool OpenedLocalContext (append-only); ShallowClone copies it
│       └── BaseRunner.cs                          # EnterSubGraph: scoped branch (Begin + seed local);
│                                                  #   HandleEndNode: End on popping a scope-opening frame
└── Tests/EditMode/Execution/
    ├── ScopedContextOverlayTests.cs               # BaseContext overlay: routing, isolation, fall-through, undeclared→local
    ├── ScopedSubGraphRunnerTests.cs               # runner opens/discards local in lockstep; sequential reuse
    ├── ScopedContextBackCompatTests.cs            # inherit/fresh unchanged; OpensScope=false ⇒ no overlay
    └── ScopedContextHistoryTests.cs               # step-back/checkpoint across a scope boundary restores overlay state
```

**Structure Decision**: No new package or assembly — this is a surgical evolution of graphcore's existing
Runtime. The overlay is added to `BaseContext` rather than a new `ScopedContext` subclass so the runner
(which holds `BaseContext`) needs no type checks and downstream typed-context subclasses inherit the
capability for free.

## Phase 0 — Research

See [research.md](research.md): R1 two flat contexts vs. scope stack, R2 overlay-in-`BaseContext` vs. new
subclass, R3 resolve-and-write routing (no `ParameterData` change), R4 the `OpensScope` flag + precedence
over `InheritParentContext`, R5 runner lockstep (seed-on-enter / discard-on-end), R6 history/step-back
overlay capture, R7 semver/back-compat strategy, R8 headless test execution.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): exact field/method changes to `BaseContext`, `SubGraphNodeData`,
  `GraphExecutionState`, `BaseRunner`; routing & lifecycle invariants.
- [contracts/public-api.md](contracts/public-api.md): authoritative new public surface + testable
  invariants (routing table, lockstep, back-compat, step-back).
- [quickstart.md](quickstart.md): how an author flags a scope-opening sub-graph and declares globals vs.
  locals; how gameflow maps Global→global context and Scene→local context.

## Implementation Sequencing (TDD, by user-story priority)

1. **P1 — Local overlay in `BaseContext`** (US1 + US2 storage layer): failing `ScopedContextOverlayTests`
   first → implement `_local`/`_localActive`, `BeginLocalContext(seedFrom?)`/`EndLocalContext`/
   `HasLocalContext`, overlay-aware `Set/Get/TryGet/Has` (resolve-and-write), `GetAllParameters`.
2. **P1 — Runner lockstep** (US1 + US2 wiring): failing `ScopedSubGraphRunnerTests` → add
   `SubGraphNodeData.OpensScope`, `GraphExecutionState.OpenedLocalContext`, scoped branch in
   `EnterSubGraph` (seed local from target graph), `EndLocalContext` in `HandleEndNode` on scope-opening
   frames; sequential reuse gives a fresh local each time.
3. **P1 — Back-compat lock** (US3): `ScopedContextBackCompatTests` → assert inherit/fresh paths untouched,
   `OpensScope=false` opens no overlay, and run the **entire pre-existing suite unmodified** (must be green).
4. **P1 — History/step-back** (FR-010): failing `ScopedContextHistoryTests` → extend `DeepClone`/
   `CopyValuesFrom` to capture & restore the overlay + open flag; confirm step-back across a scope boundary.
5. **Finalize**: bump `package.json` to 0.3.0; XML docs on all new public API; batchmode run = full suite
   green; semver assessment note.

## Complexity Tracking

> No constitution violations — section intentionally empty.
