# Implementation Plan: code-first graph ergonomics (slice 4)

**Branch**: `023-graph-builder` | **Date**: 2026-06-10 | **Spec**: [spec.md](spec.md)

**Input**: `specs/023-graph-builder/spec.md`

## Summary

Three additive ergonomics fixes from round-2 dogfooding. **(1)** A public fluent **`GraphBuilder<TGraph>`** in
`com.faolline.graphstandard` (Runtime) that builds any `BaseGraph` subclass over graphcore's universal types
(Start/Statement/Choice/SubGraph/End, edges, enter/exit actions, entry conditions, await-signal, wait,
checkpoint, choices, entry node) with auto-GUID ids — no boilerplate. **(2)** A new graphstandard **Editor**
assembly with `GraphAssetBuilder.Save(graph, path)` that persists a graph as an asset with its attached
actions/conditions as **sub-assets**. **(3)** gameflow's `GraphFlowDriver` gains `IsWaitingForTime` /
`WaitRemaining` / `WaitTotal`, symmetric with the slice-3 signal query, computed driver-side from
`OnWaitingForTime` + accumulated `Tick` (no graphcore change). **(4)** Docs bless the cyclic no-End shell
graph. graphstandard `0.3.0 → 0.4.0`, gameflow `0.3.0 → 0.4.0`; **graphcore untouched (code + README)**; the
661 EditMode + 9 PlayMode tests stay green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: `com.faolline.graphcore` 0.6.0 (the
universal `BaseGraph`/node/edge/action/condition types; the runner for the time query). **Storage**: the
persist util writes `.asset` files via `AssetDatabase` (editor only). **Testing**: NUnit EditMode — builder
structure + run-under-driver; persist util (build → save → assert sub-assets → clean up); driver time query
(feed `Tick`, assert remaining → 0). **Target Platform**: Unity runtime (builder) + Editor (persist util).
**Project Type**: buffer-lib helper + host-lib query. **Constraints**: graphcore untouched; additive/
append-only; `[GraphStandard]`/`[GraphGameFlow]` prefixes; one class per file; XML docs. **Scope**:
graphstandard Runtime builder (2–3 files) + new graphstandard Editor asmdef + util; gameflow driver edit;
tests; READMEs/CHANGELOGs; two package bumps.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore untouched (code + README). graphstandard additive 0.3.0 → 0.4.0 (new builder + new Editor asm); gameflow additive 0.3.0 → 0.4.0 (driver query). Append-only. 661 EditMode + 9 PlayMode stay green. |
| II. Universal Abstractions Only | ✅ PASS | The builder constructs ONLY graphcore's universal node/edge/action/condition model — zero domain vocabulary. graphstandard is the buffer lib for exactly this kind of domain-neutral helper. |
| III. Specification-First | ✅ PASS | spec.md approved (16/16). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Tests-first: builder produces the exact structure + a built graph runs under a driver; persist util writes sub-assets; driver time query counts down. All EditMode. |
| V. Simplicity (YAGNI) | ✅ PASS | One new abstraction (the builder), justified by a need hit in BOTH dogfooding rounds. No gameflow `.LoadScene()` sugar (deferred); the time query reuses the existing `OnWaitingForTime` + `Tick`. |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context change. |
| VII. Cross-lib via SubGraph only | ✅ PASS | The builder's SubGraph node references a `BaseGraph` (the sanctioned mechanism); no new cross-lib coupling. graphstandard depends only on graphcore. |
| Dev standards | ✅ PASS | `[GraphStandard]`/`[GraphGameFlow]` prefixes; one class per file; C# `Action<T>`; XML docs; READMEs/CHANGELOGs. |

**Result**: PASS — no violations, no deviations.

## Project Structure

```text
com.faolline.graphstandard/                          # 0.3.0 → 0.4.0
├── package.json
├── Runtime/Builder/
│   ├── GraphBuilder.cs                              # NEW: GraphBuilder<TGraph> + static Create<TGraph>()
│   └── GraphNodeBuilder.cs                          # NEW: fluent node handle (Title/At/OnEnter/OnExit/When/Await/Wait/Checkpoint/Choice/AsEntry/To)
├── Editor/
│   ├── com.faolline.graphstandard.Editor.asmdef      # NEW (refs graphcore.Runtime + graphstandard.Runtime)
│   └── GraphAssetBuilder.cs                          # NEW: Save(graph, path) → asset + actions/conditions as sub-assets
└── Tests/EditMode/
    ├── com.faolline.graphstandard.Tests.EditMode.asmdef   # MODIFIED: + graphstandard.Editor ref
    ├── GraphBuilderTests.cs                          # NEW: structure + run-under-runner
    └── GraphAssetBuilderTests.cs                     # NEW: persist → sub-assets → reload

com.faolline.graphgameflow/                          # 0.3.0 → 0.4.0
├── package.json
├── Runtime/Driver/GraphFlowDriver.cs                # MODIFIED (additive): IsWaitingForTime / WaitRemaining / WaitTotal + Tick tracking
└── Tests/EditMode/GraphFlowDriverTests.cs           # MODIFIED: the time-wait query test

# com.faolline.graphcore/ : UNCHANGED (code AND README).
```

**Structure Decision**: the builder is universal → it lives in graphstandard Runtime (option B). The persist
util is editor-only → a new graphstandard Editor assembly (graphcore's editor stays untouched). The time
query is host-specific → gameflow's driver. The cyclic-shell note is documentation in the gameflow README +
the builder docs (graphcore's README is not touched).

## Phase 0 — Research

See [research.md](research.md): R1 builder shape (generic `GraphBuilder<TGraph>` + `GraphNodeBuilder` handles,
auto-GUID, auto-column position, `Edge`/`To`, `AsEntry`/first-Start default); R2 the persist util (scan
enter/exit actions + entry conditions + choice conditions; `AddObjectToAsset` only for in-memory instances,
skip existing assets; `CreateAsset` + save); R3 driver time-remaining computed from `OnWaitingForTime`
(duration) minus accumulated `Tick`, guarded by `IsWaitingForTime`, clamped at zero — no graphcore API; R4
the cyclic no-End shell is already runner-supported (follows the single out-edge), so doc-only.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **US1 builder (test-first)**: failing `GraphBuilderTests` (build a flow with each node type + edges +
   await/wait/checkpoint + an attached action + choices + entry node ⇒ assert the exact structure; a built
   graph runs under a `BaseRunner` like the hand-built equivalent) → `GraphBuilder<TGraph>` +
   `GraphNodeBuilder`. RED → GREEN.
2. **US2 persist util (test-first)**: new graphstandard Editor asmdef; failing `GraphAssetBuilderTests` (build
   a graph with attached actions, `GraphAssetBuilder.Save` to a temp path, reload, assert the actions are
   sub-assets; clean up) → `GraphAssetBuilder.Save`. RED → GREEN. (Update the graphstandard Tests asmdef to
   reference the new Editor asmdef.)
3. **US3 driver time query (test-first)**: failing test in `GraphFlowDriverTests` (a `WaitDuration` node:
   `IsWaitingForTime` true while parked, `WaitTotal` = duration, `WaitRemaining` decreases as `Tick` is fed
   and reaches 0; false/0 before boot, after end, and off a timed node) → add `IsWaitingForTime` /
   `WaitRemaining` / `WaitTotal` + the `Tick`/`OnWaitingForTime` tracking on `GraphFlowDriver`. RED → GREEN.
4. **US4 doc + finalize**: gameflow README (cyclic-shell pattern) + graphstandard README (the builder + the
   persist util) + builder docs note the shell; CHANGELOGs; bump graphstandard 0.4.0 + gameflow 0.4.0; run
   the full suite (661 EditMode + new + 9 PlayMode) green; verify prefixes + XML docs + append-only.

## Complexity Tracking

> No violations — empty.
