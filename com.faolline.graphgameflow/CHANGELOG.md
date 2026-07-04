# Changelog

All notable changes to **com.faolline.graphgameflow** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.8.1]

### Fixed
- **Declared the `com.faolline.graphsave` dependency (≥ 0.5.0).** The Runtime assembly has always referenced
  `com.faolline.graphsave.Runtime` (the `GraphFlowDriver.Boot(GraphRunSnapshot, …)` restore path), but the
  manifest never declared it — installing graphgameflow without graphsave failed to compile. The 0.5.0 floor
  is the first graphsave that restores the raised-signal history, which the load-game path relies on.

## [0.8.0]

### Added
- **"Await Any Of (OR)" field in the node inspector.** Surfaces graphcore 0.26.0's multi-signal await
  (`BaseNodeData.AwaitSignals`) so a flow node can wait for several signals at once (resume on the first that
  passes the resume conditions) directly from the editor. Requires graphcore ≥ 0.26.0.

## [0.7.0]

### Added
- **`Boot(GraphRunSnapshot)`** — restores a flow from a saved snapshot: applies the snapshot to the
  context, then starts the runner at the saved node. Closes the save/restore loop on the driver (the
  "load game" path). Requires `com.faolline.graphsave`.
- **`UseUnscaledTime`** option (inspector toggle, default off). When enabled, the driver uses
  `Time.unscaledDeltaTime` instead of `Time.deltaTime`, so flows keep running at `timeScale = 0`
  (pause menus, cutscene overlays).
- **`ContextTrigger` target driver.** A new optional `Target Driver` field (serialized reference) lets a
  trigger target a specific per-scene `GraphFlowDriver` instead of the persistent `Active` singleton.
  Falls back to `Active` when unset.

## [0.6.3]

### Changed
- **GameFlow editor registers for GraphLink navigation.** `GameFlowGraphEditorWindow` now registers itself with
  `GraphEditorWindowRegistry` (and exposes `Open(GameFlowGraph)`), so double-clicking a `GraphLinkNodeData` that
  targets a `GameFlowGraph` opens it in the GameFlow editor. Dependency floor: graphcore `0.17.0` → `0.18.0`.

## [0.6.2]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphcore` floor `0.14.0` → `0.17.0` to match
  the current ecosystem. No code change.

## [0.6.1]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphcore` floor to `0.14.0` to match the
  current ecosystem (the editor graph view already uses graphcore 0.14.0's shared Add-Start action). No runtime
  change.

## [0.6.0]

### Added
- **`GraphFlowDriver.ChooseById(id)`** — selects a choice branch on the running flow (no-op when not running),
  mirroring `Advance`/`RaiseSignal`; a host no longer needs to reach into `Runner` to pick a choice.

### Changed
- **`AutoAdvance` no longer auto-resolves a choice.** A `ChoiceNodeData` now **pauses** for a deliberate
  `ChooseById` instead of being auto-advanced by "first passing edge" (a round-6 footgun). Non-choice nodes
  auto-advance exactly as before. (Verified safe: no prior flow/test relied on choice auto-resolution.)
- Dependency `com.faolline.graphcore` `0.6.0 → 0.7.0` (coherence; gameflow uses no 0.7.0-only API).

### Notes
- Additive MINOR (plus the choice-pause behavior fix). Pairs with graphdialoguesystem `0.3.0`'s
  `DialoguePresenter` to host a *rendered* dialogue subgraph in ~10 lines (the consumer composes; no dependency
  is added between gameflow and the dialogue lib). From round-6 dogfooding.

## [0.5.0]

### Added
- `GraphFlowDriver.Boot(GameFlowContext context, NodeExecutorRegistry registry)` — boot on a caller-prepared
  context (pre-seeded collections/parameters/services) and registry (custom node executors). A supplied
  context is used as-is (NOT re-initialised from the graph, so seeds survive; its scene loader is filled only
  when absent); nulls fall back to the defaults. `Boot()` is unchanged (equivalent to `Boot(null, null)`).
  This is the seam for hosting a progression/ability system (ReactiveEvaluator / FlowRunner) on the driver's
  shared context. From round-3 dogfooding (the only, forward-looking finding).

### Notes
- Additive (MINOR), append-only — `Boot()` and every other member unchanged. graphcore + graphstandard
  untouched. EditMode 673 green; PlayMode 9.

## [0.4.0]

### Added
- Driver **timed-wait query**, symmetric with the slice-3 signal query: `IsWaitingForTime`, `WaitRemaining`
  (never negative), `WaitTotal` — so a scene that loads during a timed node can drive a synced countdown.
  Computed driver-side from `OnWaitingForTime` + accumulated `Tick`; graphcore untouched.

### Docs
- Documented the cyclic no-End **game-shell** pattern (a looping Linear graph never ends; bound history with a
  small `HistoryDepth`). From round-2 dogfooding.

### Notes
- Additive (MINOR), append-only. graphcore + graphstandard runtime untouched. EditMode 667 green; PlayMode 9.

## [0.3.0]

### Added
- **Cross-scene persistence** on `GraphFlowDriver`: `PersistAcrossScenes` (default off) keeps the driver alive
  across single-mode scene loads (`DontDestroyOnLoad`) so one driver runs a graph that spans scenes; a
  duplicate per-scene copy self-destructs, leaving the original. A static `GraphFlowDriver.Active` lets scene
  scripts reach the persistent driver without their own singleton.
- `OnWaitingForTime` event on the driver (node + duration), symmetric with `OnWaitingForSignal`.
- `BootOnStart` toggle (default on) — turn off to disable auto-boot on Play and `Boot()` explicitly.
- `IsWaitingForSignal` / `CurrentAwaitSignal` read-only members to recover a wait that fired during a load.

### Fixed
- The documented multi-scene flow could not actually run: a single driver doing `LoadScene(Single)` was
  destroyed by its own scene load. Found by dogfooding (an escape-room as one graph spanning three scenes).
- **Added the missing real cross-scene PlayMode test** (`CrossSceneSurvivalTests`): it performs real
  `SceneManager` single-mode loads and proves the persistent driver + flow survive and complete. The slice-1/2
  stub loader recorded loads without tearing scenes down — which is exactly why this shipped green before.

### Notes
- Additive (MINOR): graphcore + graphstandard untouched; the slice-1/2 driver API is append-only and
  source-compatible (persist OFF by default ⇒ unchanged behavior). EditMode 661 green; PlayMode 9 green.

## [0.2.0]

### Added
- **Editor authoring** for gameflow, mirroring the StarterGraph editor:
  - `GameFlowGraph : BaseGraph` — a creatable graph asset (Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow Graph);
    a `BaseGraph`, so the slice-1 `GraphFlowDriver` accepts it unchanged.
  - `GameFlowGraphEditorWindow` + `GameFlowGraphView` + node views (Start/Statement/Choice/SubGraph/End) +
    `GameFlowEdgeView`: a visual canvas to add/connect/move nodes (opens on double-click; Save + Validate
    toolbar), reusing graphcore's editor infrastructure.
  - `GameFlowNodeInspectorView`: edits a node's actions (drop in a Load Scene), conditions, checkpoint, and a
    **Flow** foldout for the await-signal name and wait duration; plus End / SubGraph / Choice sections.
  - `GameFlowSampleBuilder` (Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample): generates the
    runnable reference flow (start → load A → await "advance" → load B → end) as a `GameFlowGraph` asset.
- `[CreateAssetMenu]` on `LoadSceneAction` (Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load Scene).
- `LoadSceneActionEditor`: a custom inspector for `LoadSceneAction` — pick the scene from a dropdown of the
  project's scenes (no typing), an inline Single-vs-Additive explanation, and a Build-Settings check with a
  one-click "Add to Build Settings". Runtime is unchanged (still stores the scene name).

### Notes
- Additive (MINOR): graphcore + graphstandard untouched; the slice-1 runtime is unchanged and source-
  compatible. EditMode 659 green (654 + 5 new editor/data tests); the slice-1 8 PlayMode stay green.

## [0.1.0]

### Added
- Initial release of the orchestrator / host layer above `com.faolline.graphcore` — the adapter that runs
  the headless graph runtime inside a live Unity scene.
- **GraphFlowDriver** (`MonoBehaviour`): owns a shared `GameFlowContext`, boots and drives the Linear
  `BaseRunner`, forwards `Update`'s `deltaTime` to `Tick`, exposes `RaiseSignal`/`Advance`/`Stop`, and
  re-exposes the runner's lifecycle as C# events (`OnNodeEntered`/`OnNodeCompleted`/`OnEnded`/`OnStuck`/
  `OnWaitingForSignal`). All logic is in public methods (thin `Start`/`Update`/`OnDestroy`), so the wiring is
  EditMode-testable. Auto-advance and manual-advance both supported.
- **LoadSceneAction** (`BaseAction`, NOT a node type): loads a Unity scene (Single/Additive) when it runs,
  attachable to any node's enter or exit list. Resolves the loader from the running `GameFlowContext`.
- **ISceneLoader** seam + **UnitySceneLoader** (default): keeps all driver wiring and the full
  start → load A → await → load B → end reference flow in deterministic EditMode tests, with PlayMode
  reserved for the real Unity pump.
- **GameFlowContext** (`BaseContext` subclass) + **GameFlowContextKeys**: the shared blackboard (carries the
  scene loader), with clone overrides per the Typed Context Contract.

### Notes
- graphcore and graphstandard are unchanged; the existing 634-test EditMode suite stays green (654 with this
  package's 20 EditMode tests). PlayMode adds 2 tests proving the real `Start`/`Update` pump.
