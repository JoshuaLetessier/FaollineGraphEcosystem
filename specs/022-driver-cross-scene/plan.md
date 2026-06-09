# Implementation Plan: gameflow driver cross-scene hardening (slice 3)

**Branch**: `022-driver-cross-scene` | **Date**: 2026-06-10 | **Spec**: [spec.md](spec.md)

**Input**: `specs/022-driver-cross-scene/spec.md`

## Summary

Harden `GraphFlowDriver` (com.faolline.graphgameflow) for the real multi-scene case, all additive
(**0.2.0 → 0.3.0**): an opt-in `_persistAcrossScenes` flag (`DontDestroyOnLoad` in `Awake`, default OFF) with
a single-driver guard so one driver survives single-mode scene loads and keeps running a graph that spans
scenes; a static `GraphFlowDriver.Active` accessor so scene scripts reach it without a hand-written singleton;
a re-exposed `OnWaitingForTime` event (symmetric with `OnWaitingForSignal`); a `_bootOnStart` toggle so `Start`
doesn't auto-boot when a test/integrator wants to configure and boot explicitly; and `IsWaitingForSignal` /
`CurrentAwaitSignal` read-only members so a late-subscribing scene can recover a wait that fired during a
load. The keystone is **FR-008**: a **real** cross-scene PlayMode test that actually loads scenes via
`SceneManager` (single mode) and proves a persistent driver + its in-progress flow survive and reach the end —
the test that was missing because the slice-1/2 stub loader recorded loads without ever tearing a scene down.
graphcore/graphstandard untouched; the slice-1/2 driver API stays append-only; 659 EditMode + 8 PlayMode stay
green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: `com.faolline.graphcore` 0.6.0 (the
runner already exposes `OnWaitingForTime`, `RunnerState.WaitingForSignal`, `CurrentNode`, `RaiseSignal`,
`Tick`). `UnityEngine.SceneManagement` (real loads), `DontDestroyOnLoad`. **Storage**: none. **Testing**:
NUnit — EditMode (stub loader) for the boot/event/query additions; **PlayMode with REAL `SceneManager`** for
the cross-scene survival regression (committed minimal test scenes registered in Build Settings at edit time).
**Target Platform**: Unity runtime + Editor. **Project Type**: host package hardening. **Constraints**:
graphcore/graphstandard untouched; slice-1/2 API append-only; `[GraphGameFlow]` prefix; one class per file;
C# `Action<T>` (no `UnityEvent`); XML docs. **Scope**: edits to `GraphFlowDriver.cs` only (runtime) + new
PlayMode test + two committed test scenes + README/CHANGELOG + package bump.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore + graphstandard untouched. gameflow additive 0.2.0 → 0.3.0; the driver API is append-only (new serialized fields, one new event, new read-only members; `Boot`/`Tick`/`Advance`/`RaiseSignal`/`Stop` unchanged). 659 EditMode + 8 PlayMode stay green. |
| II. Universal Abstractions Only | ✅ PASS | gameflow is the host layer; `DontDestroyOnLoad` / `SceneManager` are Unity concerns that belong here (spec 020 FR-010). No domain vocabulary. |
| III. Specification-First | ✅ PASS | spec.md approved (16/16). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Tests-first: EditMode (stub) for `bootOnStart`, `OnWaitingForTime`, `IsWaitingForSignal`/`CurrentAwaitSignal`; the **PlayMode cross-scene regression is written first** and is the whole point of the slice. The "EditMode-only/PlayMode-never" wording is graphcore-scoped; this slice exists *because* a real PlayMode test was missing. |
| V. Simplicity (YAGNI) | ✅ PASS | Option A (a flag + a static accessor), not a packaged bootstrap component. Minimal additions to one file. |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context change. |
| VII. Cross-lib via SubGraph only | ✅ PASS | No cross-lib mechanism; depends only on graphcore. |
| Dev standards | ✅ PASS | `[GraphGameFlow]` prefix; `Action<T>` (no `UnityEvent`); XML docs. `GraphFlowDriver.cs` stays one class per file (we edit it, not split it). `MonoBehaviour`/`DontDestroyOnLoad` are the host layer's remit. |

**Result**: PASS. One mild opinion (the persist flag also dedups duplicate persistent drivers) is justified in
Complexity Tracking.

## Project Structure

```text
com.faolline.graphgameflow/
├── package.json                                   # 0.2.0 → 0.3.0
├── Runtime/Driver/GraphFlowDriver.cs              # MODIFIED (additive): persist flag + Active + OnWaitingForTime + bootOnStart + waiting query
└── Tests/
    ├── EditMode/GraphFlowDriverTests.cs           # MODIFIED: bootOnStart, OnWaitingForTime, IsWaitingForSignal/CurrentAwaitSignal (stub loader)
    └── PlayMode/
        ├── Scenes/GameFlowCrossSceneA.unity        # NEW: minimal committed test scene (generated at edit time)
        ├── Scenes/GameFlowCrossSceneB.unity        # NEW: minimal committed test scene
        └── CrossSceneSurvivalTests.cs              # NEW: REAL SceneManager single-mode loads; persistent driver survives + completes

# graphcore/ and graphstandard/ : UNCHANGED.  Slice-1/2 runtime API: append-only.
```

**Structure Decision**: all runtime change is in the existing `GraphFlowDriver.cs` (additive); the regression
lives in a new PlayMode test backed by two committed empty scenes registered into Build Settings by a
`UNITY_EDITOR`-guarded `[OneTimeSetUp]` (and removed in teardown).

## Phase 0 — Research

See [research.md](research.md): R1 persistence shape (`DontDestroyOnLoad` in `Awake` + single-driver guard +
static `Active`); R2 setting the flag before `Awake` in tests (inactive-GameObject pattern, since `AddComponent`
runs `Awake` immediately); R3 the real cross-scene test mechanism (committed empty scenes generated at edit
time via an `-executeMethod` helper; registered into `EditorBuildSettings` in a guarded `[OneTimeSetUp]`; real
`SceneManager.LoadScene(Single)`); R4 waiting-state query derived from runner state (no new runner API); R5
`OnWaitingForTime` is a pure re-expose (runner already raises it).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **EditMode additions (test-first, stub loader)**: extend `GraphFlowDriverTests` → `_bootOnStart=false`
   suppresses `Start` auto-boot (no "already running" warning on an explicit `Boot`); subscribing to
   `OnWaitingForTime` fires for a `WaitDuration` node; `IsWaitingForSignal` is true and `CurrentAwaitSignal`
   reports the name while parked on an await node, false/"" otherwise and before boot/after end. Implement the
   additive members on `GraphFlowDriver`. RED → GREEN.
2. **PlayMode cross-scene regression (the keystone)**: generate two minimal empty scenes
   (`Tests/PlayMode/Scenes/GameFlowCrossSceneA/B.unity`) at edit time; write `CrossSceneSurvivalTests`
   (`[OneTimeSetUp]` registers them in Build Settings, teardown removes) → a persistent driver
   (`PersistAcrossScenes=true`, set before `Awake` via the inactive-GameObject pattern) running
   `start → loadA(Single) → await "advance" → loadB(Single) → end` with the **real** `UnitySceneLoader`: after
   boot scene A is the active scene and the driver is alive + parked; `RaiseSignal("advance")` loads B and the
   flow reaches `OnEnded` with the driver **still alive**; `GraphFlowDriver.Active` points to it. Confirm it
   FAILS without the persist flag (the diagnostic) then GREEN with it.
3. **Persistence + accessor**: `Awake` → if `_persistAcrossScenes`: dedup against `Active` (a duplicate
   per-scene copy destroys itself, leaving the original running) else set `Active = this` +
   `DontDestroyOnLoad`; `OnDestroy` clears `Active` when it owned it. RED → GREEN on the PlayMode test.
4. **Back-compat + finalize**: run the entire 659 EditMode (unchanged behavior with persist OFF) + the new
   EditMode + the new PlayMode (9 PlayMode) green; bump `0.3.0`; update README (cross-scene persistence
   pattern + the SubGraph-decomposition note + `OnWaitingForTime`/`bootOnStart`/waiting-query) + CHANGELOG.

## Complexity Tracking

| Decision | Why | Simpler alternative rejected because |
|----------|-----|--------------------------------------|
| The persist flag also **dedups** duplicate persistent drivers (a per-scene copy self-destructs, the original keeps running) | Games often embed the driver in every scene so each scene is runnable standalone in the editor; without dedup, every scene load would stack another `DontDestroyOnLoad` driver. This bakes in the consumer's validated workaround. | *Plain `DontDestroyOnLoad`, no guard* — leaves duplicate persistent drivers and ambiguous `Active`. The guard is a few lines and matches the real usage the dogfood revealed. |
