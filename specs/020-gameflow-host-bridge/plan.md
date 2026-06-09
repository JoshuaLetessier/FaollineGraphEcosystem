# Implementation Plan: gameflow host bridge + Linear scene-flow (vertical slice 1)

**Branch**: `020-gameflow-host-bridge` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: `specs/020-gameflow-host-bridge/spec.md`

## Summary

The first slice of a fresh **`com.faolline.graphgameflow`** package: the **host bridge** that runs the
headless graphcore runtime inside a live Unity scene. A `GraphFlowDriver` MonoBehaviour references a
`BaseGraph` asset, owns a shared `GameFlowContext`, boots a graphcore `BaseRunner`, forwards `Update`'s
`deltaTime` into `runner.Tick`, exposes `RaiseSignal` / `Advance`, and re-exposes the runner's lifecycle
events as C# `Action` hooks. A `LoadSceneAction` (a graphcore `BaseAction`, NOT a node type) loads a Unity
scene when it runs — attachable to any node's enter/exit list. Scene loading goes through an `ISceneLoader`
seam (`UnitySceneLoader` by default, a stub in EditMode) so the driver wiring and the full scene-flow logic
are EditMode-testable and PlayMode is reserved for the one genuine `SceneManager` path. The package is new
and additive (**0.1.0**, depends on `com.faolline.graphcore` 0.6.0); graphcore and graphstandard are
untouched and the existing 634-test EditMode suite stays green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: `com.faolline.graphcore` 0.6.0
(`BaseGraph`, `BaseRunner`, `BaseContext`, `BaseAction`, `NodeExecutorRegistry`, the signal channel and
`Tick`). Unity modules: `UnityEngine` (MonoBehaviour) and `UnityEngine.SceneManagement` (Runtime),
`UnityEditor` only for PlayMode test scene registration. **Storage**: none (in-memory run state).
**Testing**: NUnit via Unity batchmode — **EditMode** for driver wiring + `LoadSceneAction` (against a stub
`ISceneLoader`), **PlayMode** for the real `SceneManager` load path (the ecosystem's first PlayMode tests).
**Target Platform**: Unity runtime (any player) + Editor. **Project Type**: orchestrator / host package
above the headless libs. **Performance Goals**: a driver tick is O(1); booting is O(graph). **Constraints**:
graphcore/graphstandard untouched; `[GraphGameFlow]` log prefix; one class per file; XML docs; C# `Action<T>`
(no `UnityEvent`). **Scale/Scope**: one driver, one scene-load action, one seam interface + default impl,
one reference scene-flow; ~6 Runtime files + 3 test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

> The constitution is titled for `com.faolline.graphcore` and "supersedes all other project practices for
> com.faolline.graphcore." Its principles bind the **headless foundation**; `graphgameflow` is the
> **host/adapter layer** whose explicit job (spec FR-010) is to bind that foundation to the Unity runtime.
> Three rules written *for the core* are therefore intentionally inverted here and justified in Complexity
> Tracking; everything else is honored.

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore & graphstandard untouched. New additive package at 0.1.0. The 634-test EditMode suite stays green by construction; gameflow adds its own. |
| II. Universal Abstractions Only | ⚠️ DEVIATION (justified) | This rule governs graphcore. gameflow is the one layer where Unity/domain vocabulary lives by design (scene component, `SceneManager`, scene-load action). The libs beneath stay universal. See Complexity Tracking #1/#3. |
| III. Specification-First | ✅ PASS | spec.md approved (16/16 checklist), incl. the locked "scene = action, not node" decision. |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS (with scoped PlayMode) | Failing tests first. EditMode covers driver wiring + action via the stub seam; PlayMode covers the real `SceneManager` path. The "EditMode only / Coplay MCP" wording is graphcore-scoped; gameflow legitimately needs PlayMode (minimized via the seam) and runs via the project's Unity batchmode harness. See Complexity Tracking #2. |
| V. Simplicity (YAGNI) | ✅ PASS | One MonoBehaviour, one action, one seam (`ISceneLoader`) + default + stub. The seam is the single abstraction, justified by deterministic EditMode testing (the simpler "PlayMode-only" alternative is slow and flaky). |
| VI. Typed Context Contract (NON-NEGOTIABLE) | ✅ PASS | `GameFlowContext : BaseContext` overrides `CreateCloneInstance()` + `DeepClone()` (carrying the `ISceneLoader` service reference). `GameFlowContextKeys` placeholder ships; no domain string-key literals at call sites (none exist yet). |
| VII. Cross-lib via SubGraph only | ✅ PASS | Depends only on graphcore (the foundation), never a sibling lib. No new cross-lib mechanism. |
| Dev standards | ✅ PASS (MonoBehaviour excepted) | `[GraphGameFlow]` prefix; one class per file; XML docs; C# `Action<T>`, **no `UnityEvent`**. `MonoBehaviour` IS used (the driver) — the justified core-rule inversion, Complexity Tracking #1. |

**Result**: PASS — the three deviations are inherent to the host layer's purpose and justified below.

## Project Structure

### Documentation (this feature)

```text
specs/020-gameflow-host-bridge/
├── plan.md              # This file
├── research.md          # Phase 0 — R1..R6
├── data-model.md        # Phase 1 — entities + state
├── quickstart.md        # Phase 1 — integrator walkthrough
├── contracts/
│   └── public-api.md     # Phase 1 — driver / action / seam / context API
├── checklists/
│   └── requirements.md   # spec quality checklist (done)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
com.faolline.graphgameflow/                       # NEW additive package, 0.1.0
├── package.json                                  # depends on com.faolline.graphcore 0.6.0 (pinned)
├── README.md                                     # ships from day one (incl. driver quickstart)
├── CHANGELOG.md                                  # ships from day one
├── Runtime/
│   ├── com.faolline.graphgameflow.Runtime.asmdef # refs graphcore.Runtime; SceneManagement module
│   ├── Context/
│   │   ├── GameFlowContext.cs                     # BaseContext subclass; carries ISceneLoader; clone overrides
│   │   └── GameFlowContextKeys.cs                 # keys placeholder (no domain keys yet)
│   ├── Scene/
│   │   ├── ISceneLoader.cs                        # seam: LoadScene(name, mode)
│   │   ├── UnitySceneLoader.cs                    # default impl → UnityEngine.SceneManagement
│   │   └── LoadSceneAction.cs                     # BaseAction; serialized sceneName + LoadSceneMode
│   └── Driver/
│       └── GraphFlowDriver.cs                     # MonoBehaviour host bridge
└── Tests/
    ├── EditMode/
    │   ├── com.faolline.graphgameflow.Tests.EditMode.asmdef
    │   ├── StubSceneLoader.cs                     # records LoadScene calls; deterministic
    │   ├── GraphFlowDriverTests.cs               # boot, tick fwd, signal fwd/resume, events, auto/manual, edge cases
    │   └── LoadSceneActionTests.cs               # action calls loader w/ right args; any node type; missing scene logs
    └── PlayMode/
        ├── com.faolline.graphgameflow.Tests.PlayMode.asmdef   # references Runtime + graphcore + test-framework
        ├── Scenes/                               # two tiny scenes registered in build settings (editor setup)
        └── SceneFlowPlayModeTests.cs             # real SceneManager: driver loads a scene on node enter under Play

# com.faolline.graphcore/ and com.faolline.graphstandard/ : UNCHANGED.
```

**Structure Decision**: A new top-level package mirroring the ecosystem layout (Runtime asmdef + EditMode +
PlayMode test asmdefs). The `ISceneLoader` seam is the seam between the headless-testable wiring and the
Unity-bound scene load: `UnitySceneLoader` is the production default carried on the context; `StubSceneLoader`
is injected in EditMode. This keeps all logic (boot, tick, signal, events, the A→await→B flow) in fast
deterministic EditMode tests and confines PlayMode to proving the real `SceneManager` call.

## Phase 0 — Research

See [research.md](research.md): R1 scene-load seam (`ISceneLoader` carried on `GameFlowContext` vs. a static
ambient / a node field); R2 reusing graphcore's await-signal + `Tick` (no new runtime semantics); R3
MonoBehaviour lifecycle vs. EditMode-callable public methods (`Boot`/`Tick`/`RaiseSignal`/`Advance` so wiring
is testable without Play); R4 auto-advance vs. await-signal interaction (await parks before `OnNodeCompleted`,
so auto-advance does not skip it); R5 deterministic PlayMode scene test (build-settings registration in
editor `OneTimeSetUp`, minimal additive scene); R6 `LoadSceneAction` as a graphcore `BaseAction` attachable
to any node's enter/exit list (the locked spec decision).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **Package skeleton (no logic)**: package.json (dep pinned 0.6.0), README + CHANGELOG stubs, the three
   asmdefs (Runtime + EditMode + PlayMode). Confirm the project compiles and the new asmdefs resolve.
2. **US2 — `LoadSceneAction` (EditMode, stub seam)**: failing `LoadSceneActionTests` → `ISceneLoader`,
   `UnitySceneLoader`, `GameFlowContext` (carrying the loader), `LoadSceneAction`. Tests: action calls the
   loader with the right name/mode; identical from a statement / choice / subgraph node's enter *and* exit
   list; missing/empty scene logs `[GraphGameFlow]` error, no throw. Confirm RED → GREEN.
3. **US1 — `GraphFlowDriver` wiring (EditMode, stub seam)**: failing `GraphFlowDriverTests` → the MonoBehaviour
   with EditMode-callable `Boot`/`Tick`/`Advance`/`RaiseSignal` + event re-exposure + auto/manual advance +
   the guards. Tests: boot enters start (or warns inert on no graph/start); auto-advance runs to End; manual
   advance only on call; `Tick(dt)` forwards (a time-wait node resolves); zero/negative dt ignored; events
   re-raised; destroyed/disabled driver does not throw. Confirm RED → GREEN.
4. **US3 — signal resume + reference flow (EditMode, stub seam)**: failing tests → the full
   start → load-A → await-"advance" → load-B → end graph runs under the driver with a `StubSceneLoader`:
   load-A recorded, flow parks, matching `RaiseSignal("advance")` resumes and records load-B; non-matching
   signal is a no-op; signal before boot / after end is a no-op. Confirm RED → GREEN.
5. **PlayMode — genuine `SceneManager` path**: failing `SceneFlowPlayModeTests` → register the tiny test
   scene(s) in build settings (editor `OneTimeSetUp`); a driver running a one-node graph whose enter-action
   loads a real additive scene results in that scene being loaded after the Play pump. Confirm RED → GREEN.
6. **Back-compat + finalize**: run the entire existing 634-test EditMode suite unchanged (graphcore/
   graphstandard untouched) + the new gameflow EditMode tests, all green; the PlayMode suite green; fill
   README (driver quickstart, "scene = action" note) + CHANGELOG; verify XML docs + `[GraphGameFlow]` prefix.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **`MonoBehaviour` in Runtime** (the driver) — inverts "No MonoBehaviour in Runtime core" | The host bridge must be droppable on a GameObject and receive Unity's `Start`/`Update` lifecycle; that is the package's entire purpose (spec FR-001/FR-003/FR-010). | A plain C# driver cannot receive the Update pump nor be assigned in a scene — it would force every game to write its own MonoBehaviour wrapper, pushing the bridge into user code and defeating the package. The rule is graphcore-scoped; the headless libs keep their no-MonoBehaviour guarantee. |
| **PlayMode tests** — inverts "EditMode only; PlayMode never required for core" | Scene loading via `SceneManager` is genuinely PlayMode-bound and cannot be exercised in EditMode. | "PlayMode-only" would be slow and flaky; instead the `ISceneLoader` seam pushes all wiring + the full A→await→B logic into fast EditMode tests, leaving PlayMode to prove only the real `SceneManager` call. The rule is graphcore-scoped (its runner is headless); gameflow is not. |
| **Unity/domain vocabulary in a lib** (scene-load action, `SceneManagement`) — inverts "Universal Abstractions Only" | gameflow is the orchestrator/adapter; binding graphs to Unity scenes is exactly its reason to exist (spec FR-010). | Staying universal would mean the layer could never load a scene at all. The universality guarantee is preserved where it matters — graphcore/graphstandard — and the boundary is explicit. |
