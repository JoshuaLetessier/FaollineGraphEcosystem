# Phase 0 — Research: gameflow host bridge

## R1 — Scene-load seam: `ISceneLoader` carried on the context

**Decision**: Define `ISceneLoader { void LoadScene(string sceneName, LoadSceneMode mode); }`. The default
`UnitySceneLoader` calls `UnityEngine.SceneManagement.SceneManager.LoadScene`. The active loader is carried
on `GameFlowContext.SceneLoader` (defaulting to a `UnitySceneLoader`). `LoadSceneAction.Execute(context)`
resolves `(context as GameFlowContext)?.SceneLoader`, falling back to a shared default, and logs a
`[GraphGameFlow]` error if it cannot load.

**Rationale**: `BaseAction.Execute` receives only a `BaseContext`, and `BaseContext` parameters are limited
to bool/int/float/string — so a service object cannot be a parameter. A typed `GameFlowContext` field is the
natural carrier and matches "the driver owns and wires the context." Per-instance (not global) state means
two drivers can have different loaders, and EditMode tests inject a `StubSceneLoader` deterministically with
no global teardown.

**Alternatives**: *a static ambient `SceneLoading.Loader`* — simplest, but global mutable state needs
careful test reset and forbids per-driver loaders; rejected. *a node field / dedicated scene node* —
rejected by the spec decision (scene = action, not node) and would touch graphcore.

## R2 — Reuse graphcore's await-signal and `Tick` (no new runtime semantics)

**Decision**: The driver adds **no** execution semantics. Await-signal is already in graphcore 0.6.0
(`BaseNodeData.AwaitSignalName`, `BaseRunner.OnWaitingForSignal`, `RaiseSignal`, `RunnerState.WaitingForSignal`);
time-wait is already there (`WaitDuration`, `Tick(float)`, `OnWaitingForTime`). The driver only *pumps* and
*forwards*: `Update → runner.Tick(Time.deltaTime)`, `RaiseSignal(name) → runner.RaiseSignal(name)`.

**Rationale**: Constitution V (use graphcore, never reimplement) and I (don't touch graphcore). The bridge is
pure wiring over a verified API; the reference flow needs no custom executor (statement/await nodes are
handled by `BaseRunner` itself), so the `NodeExecutorRegistry` is empty for this slice.

**Alternatives**: *re-implement waiting in the driver* — rejected: duplicates graphcore and violates V.

## R3 — MonoBehaviour lifecycle vs. EditMode-callable public methods

**Decision**: `GraphFlowDriver` exposes plain public methods — `Boot()`, `Tick(float dt)`, `Advance()`,
`RaiseSignal(string[, payload])` — that contain all logic. The Unity hooks are thin: `Start() => Boot()`,
`Update() => Tick(Time.deltaTime)` (guarded by run state). `autoAdvance` subscribes `OnNodeCompleted` to
`Advance`.

**Rationale**: Unity does not pump `Start`/`Update` in EditMode, so putting logic there would force PlayMode
for everything. Public methods let EditMode tests `AddComponent<GraphFlowDriver>()`, set the graph + stub
loader, then call `Boot()` / `Tick(dt)` / `RaiseSignal(...)` directly and assert — fast and deterministic.
PlayMode then only confirms the thin hooks actually fire and drive the real loader.

**Alternatives**: *logic in `Start`/`Update`* — rejected: untestable without Play. *a non-MonoBehaviour
driver + a separate pumping component* — rejected as more types for no gain; the MonoBehaviour is the bridge.

## R4 — Auto-advance vs. await-signal interaction

**Decision**: Auto-advance subscribes to `OnNodeCompleted` and calls `Advance()` (`Proceed`). An await-signal
node parks the runner in `WaitingForSignal` and raises `OnWaitingForSignal` **instead of** `OnNodeCompleted`,
so auto-advance does not skip the wait. When the matching signal arrives the runner completes the node, then
`OnNodeCompleted` fires and auto-advance proceeds.

**Rationale**: The event sequence in `BaseRunner.EnterCurrentNode` returns at the await branch before the
"pause/complete" path, so `OnNodeCompleted` is not raised while waiting — auto-advance is naturally correct
with no special-casing in the driver.

**Alternatives**: *driver inspects node type to decide whether to auto-advance* — rejected: unnecessary, the
runner's event contract already encodes it.

## R5 — Deterministic PlayMode scene test

**Decision**: Keep one minimal PlayMode test proving the real `SceneManager` path: a one-node graph whose
enter-action loads a tiny test scene **additively**; after entering Play and pumping a frame, assert the
scene is loaded (`SceneManager.GetSceneByName(...).isLoaded`). The test scene(s) live under
`Tests/PlayMode/Scenes/` and are registered into build settings in a `[OneTimeSetUp]` guarded by
`#if UNITY_EDITOR` (`EditorBuildSettings.scenes`), then removed in teardown.

**Rationale**: A real `SceneManager.LoadScene` needs a scene in build settings; additive load avoids tearing
down the test runner's own scene. Confining PlayMode to this single real-load assertion (the A→await→B logic
is EditMode via the stub) keeps PlayMode small and robust — appropriate for the ecosystem's first PlayMode
tests.

**Alternatives**: *full A→await→B in PlayMode with real scenes* — rejected: slower, more build-settings
surface, redundant with the EditMode stub coverage. *`SceneManager.CreateScene` at runtime* — rejected: does
not exercise the real `LoadScene`-by-name path the action uses.

## R6 — `LoadSceneAction` as a `BaseAction` on any node (locked spec decision)

**Decision**: `LoadSceneAction : BaseAction` (a `ScriptableObject` like every graphcore action), with
serialized `sceneName` (string) and `mode` (`LoadSceneMode` Single/Additive). It is attached to any node's
`OnEnterActions` or `OnExitActions` — never a dedicated node type.

**Rationale**: The spec locked this (US2/FR-007): a scene change is a side-effect, not a flow state.
`BaseAction` already runs on enter/exit of every node type, giving full composability (load on entering a
statement, on exiting a choice, on entering a subgraph) for free, with zero new runtime node type and
graphcore untouched.

**Alternatives**: *dedicated scene node* — rejected (redundant, non-composable, touches graphcore). Editor
affordance (highlighting the action on the canvas) can be added later as inspector sugar without changing
the model.
