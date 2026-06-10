# Implementation Plan: gameflow driver boot configuration seam (slice 5)

**Branch**: `024-driver-boot-seam` | **Date**: 2026-06-10 | **Spec**: [spec.md](spec.md)

**Input**: `specs/024-driver-boot-seam/spec.md`

## Summary

One additive overload on `GraphFlowDriver`: `Boot(GameFlowContext context, NodeExecutorRegistry registry)`.
`Boot()` is unchanged (delegates to `Boot(null, null)`); a shared internal path applies the same boot guards.
When a context is provided the driver runs on **it** (filling its `SceneLoader` only if absent) and does **not**
`InitFromGraph` over it (the caller owns seeding); a null context falls back to the current fresh-context path.
A provided registry is passed to `BaseRunner.Start` (custom executors active); a null registry → a fresh empty
one. This is the seam the next slice (hosting a Reactive progression / Flow abilities on the shared context)
needs. graphcore/graphstandard untouched; gameflow `0.4.0 → 0.5.0`; the 667 EditMode + 9 PlayMode tests stay
green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: `com.faolline.graphcore` 0.6.0
(`BaseRunner`, `NodeExecutorRegistry`, `INodeExecutor`, `BaseContext`). **Storage**: none. **Testing**: NUnit
EditMode — seeded context observed by the flow; provided registry's executor invoked; provided context not
re-initialised; `SceneLoader` filled when absent; `Boot()` unchanged. **Target Platform**: Unity runtime +
Editor. **Project Type**: host-lib additive seam. **Constraints**: graphcore/graphstandard untouched; the
slice-1..4 driver API append-only (`Boot()` unchanged; only a new overload + a private helper); `[GraphGameFlow]`
prefix; one class per file; XML docs. **Scope**: one edit to `GraphFlowDriver.cs` + EditMode tests +
README/CHANGELOG + package bump.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore + graphstandard untouched. gameflow additive 0.4.0 → 0.5.0; `Boot()` unchanged (delegates), new overload + private helper only. 667 EditMode + 9 PlayMode stay green. |
| II. Universal Abstractions Only | ✅ PASS | Host layer; no domain vocabulary. |
| III. Specification-First | ✅ PASS | spec.md approved (16/16). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Tests-first: seeded context observed, executor invoked, no-InitFromGraph, SceneLoader fill, `Boot()` unchanged. All EditMode. |
| V. Simplicity (YAGNI) | ✅ PASS | One overload + a shared private path; no `OnConfiguring` event (deferred); the overload covers the need. |
| VI. Typed Context Contract | ✅ PASS (N/A here) | The overload accepts a `GameFlowContext`; the typed-subclass-with-keys belongs to the consumer that seeds it. |
| VII. Cross-lib via SubGraph only | ✅ PASS | No cross-lib mechanism; depends only on graphcore. |
| Dev standards | ✅ PASS | `[GraphGameFlow]` prefix; XML docs; `GraphFlowDriver.cs` stays one class per file (edited, not split). |

**Result**: PASS — no violations, no deviations.

## Project Structure

```text
com.faolline.graphgameflow/
├── package.json                                   # 0.4.0 → 0.5.0
├── Runtime/Driver/GraphFlowDriver.cs              # MODIFIED (additive): Boot(context, registry) overload + shared BootInternal; Boot() delegates
└── Tests/EditMode/GraphFlowDriverTests.cs         # MODIFIED: the boot-seam tests

# com.faolline.graphcore/ and com.faolline.graphstandard/ : UNCHANGED.
```

**Structure Decision**: a single additive change to `GraphFlowDriver`. Extract the current `Boot()` body into
a private `BootInternal(GameFlowContext, NodeExecutorRegistry)`; `Boot()` calls `BootInternal(null, null)` (the
existing behavior), and the new public `Boot(context, registry)` calls `BootInternal(context, registry)`.

## Phase 0 — Research

See [research.md](research.md): R1 overload vs. settable properties vs. event (overload chosen — explicit,
minimal, append-only); R2 the provided-context contract (use as-is, fill `SceneLoader` only when null, no
`InitFromGraph` so seeded declared parameters survive); R3 the registry contract (`registry ?? new
NodeExecutorRegistry()`); R4 `Boot()` preserved by delegation, guards shared in `BootInternal`.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **Boot-seam tests (test-first)**: extend `GraphFlowDriverTests` → (a) a context seeded before boot
   (`Set<int>("seed",42)`) is the live one: `Boot(context, null)` ⇒ `driver.Context` is that instance and the
   value survives; (b) a graph declaring a parameter with default X + the provided context pre-set to Y ⇒ after
   `Boot(context,…)` the value is still Y (no `InitFromGraph`); (c) a provided context with null `SceneLoader`
   gets the driver's (a `LoadSceneAction` reaches a recording loader); a context with its own loader keeps it;
   (d) a registry with a test `INodeExecutor` for a node type ⇒ booting with it and entering such a node
   invokes the executor; (e) `Boot()` (no args) still creates a fresh context initialised from the graph + an
   empty registry (the existing tests already cover the run; assert a graph-declared default is applied);
   (f) the guards (no graph / already running) fire identically for the overload. Confirm RED.
2. **Implement the seam**: in `GraphFlowDriver.cs`, extract `BootInternal(GameFlowContext, NodeExecutorRegistry)`
   from the current `Boot()` body (guards; if context != null use it + fill `SceneLoader` when null, else fresh
   + `InitFromGraph`; subscribe; `_running = true`; `_runner.Start(_graph, _context, registry ?? new
   NodeExecutorRegistry())`); make `Boot()` call `BootInternal(null, null)`; add public
   `Boot(GameFlowContext, NodeExecutorRegistry)` calling `BootInternal`. XML docs. Confirm GREEN.
3. **Finalize**: run the full suite (667 + new EditMode, 9 PlayMode) green; bump `0.5.0`; README (a "prepare
   the context / register executors" note pointing at the overload as the foundation for hosting a
   progression/ability system on the shared context) + CHANGELOG; verify prefix + XML + append-only.

## Complexity Tracking

> No violations — empty.
