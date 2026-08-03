# Changelog

All notable changes to **com.faolline.graphgameflow** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.17.1]

### Removed — `ChapterRootSubGraphValidatorExtension`

0.17.0 shipped this to warn when a hard `SubGraphNodeData` reference targets a graph registered as a
key (e.g. marked Addressable). On review, the rule presumed *why* a graph was registered — it
treated "promoted to a key" as synonymous with "must never be hard-referenced," but a project can
legitimately register a key purely for `graphsave`/`IGraphCatalog` `GraphId` resolution with no
soft-loading intent at all, and still compose that same graph via `SubGraphNodeData` elsewhere on
purpose. The rule had no way to distinguish that case from an actual mistake, so it presumed intent
it couldn't know and was removed rather than kept as a source of false-positive warnings.
`GraphKeySourceRegistry`/`IGraphKeySourceProvider.TryResolveGuid` are unaffected (still used by
`GraphKeyRegistryWindow`); graphcore's generic `GraphValidatorExtensionRegistry` seam stays in place
(empty by default) for a future extension that can actually know the difference.

## [0.17.0]

### Added — `IGraphCatalog` port (`GraphId → BaseGraph`, independent of loading technology)

Mirrors `ISceneLoader`'s role on the graph side. `GraphRunSnapshot.GraphId` is informational only,
and `Restore` requires the caller to already have the `BaseGraph` in hand — the moment a project has
more than one independently-loadable root graph, that resolution needs a formal seam instead of a
hand-maintained lookup table. `GameFlowContext.GraphCatalog` (new property, mirrors `SceneLoader`'s
own treatment including `DeepClone`) carries it. Ships with `DirectGraphCatalog`, a zero-dependency
in-memory implementation — the seam works identically with or without any asynchronous asset-loading
technology installed.

### Added — `GraphKeySourceRegistry` / `GraphKeyRegistryWindow` (Editor)

Graph-side mirror of `SceneKeySourceRegistry`: an opt-in registry of `IGraphKeySourceProvider`s (e.g.
registered Addressable graph addresses), consumed by a new `Faolline ▸ Graph ▸ Graph Key Registry`
window listing project graphs, their `GraphId`, and per-source promotion status.

### Added — `ChapterRootSubGraphValidatorExtension`

Registers into graphcore 0.41.0's new `GraphValidatorExtensionRegistry` seam to warn when a hard
`SubGraphNodeData` reference accidentally targets a graph registered as a chapter root — that hard
reference would silently reintroduce the full build-time pull the soft-loading lots (graphcore
0.41.0, this package) exist to prevent. graphcore itself has no notion of "chapter root"; this
extension is where that meaning lives.

### Changed
- Dependency floor: `com.faolline.graphcore` raised to `0.41.0` (required for
  `GraphValidatorExtensionRegistry`).

## [0.16.1]

### Fixed
- **`GraphFlowDriver.HandleEnded` never unsubscribed.** `Subscribe()` re-attaches the driver's handlers to the
  runner AND to the static `SceneManager.sceneLoaded`/`sceneUnloaded` events on every `Boot()`, but only
  `Stop()` (called from `OnDestroy`) ever detached them. Every reboot cycle (flow ends, then `Boot()` again)
  stacked a new static subscription on top of one that was never removed — a driver that reboots repeatedly
  (e.g. a hub-and-spoke minigame flow) grew its `SceneManager` subscriber count without bound, and a later
  `OnDestroy` only removed one of the accumulated subscriptions, permanently rooting the destroyed driver
  instance via the leftover dangling delegates. `HandleEnded` now calls `Unsubscribe()` before the
  flow-ended fanout, so a reboot cleanly replaces the subscription instead of stacking a second one.
- **Reentrant `Boot()` from an `OnEnded` handler corrupted driver state.** `BaseRunner` fires `OnEnded`
  synchronously and inline (from deep inside `ExitAndAdvance`/`Tick`), so a driver `OnEnded` subscriber that
  calls `Boot()` to reboot into the next flow runs on the SAME call stack as whatever `Advance`/`Tick`/
  `RaiseSignal` triggered it — reassigning `_runner`/`_context`/`_running` out from under the still-unwinding
  outer call. Every top-level entry point (`Boot`, `Tick`, `Advance`, `ChooseById`, `RaiseSignal`) now tracks
  dispatch depth; a `Boot()` call made reentrantly is queued and safely replayed once the outermost dispatch
  has fully unwound, instead of mutating shared state mid-stack. Rebooting a driver from its own `OnEnded`
  handler — a natural pattern for flow-to-flow transitions — is now a supported, safe operation.

## [0.16.0]

### Added
- **Addressable Key dropdown on `LoadSceneAction`/`UnloadSceneAction` inspectors.** Previously the custom
  editors only offered a Build Settings scene picker, so authoring a scene action meant to run through
  `com.faolline.graphgameflow.addressables`'s `AddressablesSceneLoader` required typing the Addressable key
  by hand (easy to typo against the resulting scene's own name — the two are rarely equal, see
  `LoadSceneAction`'s loader-agnostic design note). When the Addressables package is present in the project,
  a "Build Settings / Addressable" toolbar switches the inspector to a second dropdown listing registered
  scene addresses, mirroring the existing picker (guessed from the field's current value on first draw, then
  authoring-session-persistent). A "Mark as Addressable" button — the Addressable-side mirror of "Add to
  Build Settings" — promotes a plain project scene to an Addressable entry (default group, address = scene
  name) in one click. Zero hard dependency added: gated behind a `FAOLLINE_ADDRESSABLES` Version Define on
  the Editor asmdef, so the core package stays dependency-free when Addressables isn't installed. Shared
  dropdown/build-settings-check logic between the two editors extracted into `SceneNameFieldDrawer` (was
  duplicated verbatim between them).

## [0.15.2]

### Fixed
- **Bumped the `com.faolline.graphsave` floor to `0.8.0`.** Stale at `0.7.1` — one minor release behind
  (`JsonFileGraphSaveStore` now rejects an invalid slot name instead of sanitizing it). No code change here.

## [0.15.1]

### Fixed
- **Bumped the `com.faolline.graphsave` floor to `0.7.1`.** Stale at `0.5.0` — two minor releases behind.
  Found while fixing graphsave's corrupt-file/long-slot-name hardening (0.7.1). No code change here.

## [0.15.0]

### Fixed
- **A load/unload that fails INSTANTLY (bad name, not in Build Settings) could raise its `LoadFailedSignal`/
  `UnloadFailedSignal` too early to be caught live.** `LoadSceneAction.Execute` and the runner's own
  auto-advance are both fully synchronous C# calls; a `LoadScene`/`UnloadScene` call whose failure guard
  rejects the request without ever starting real async work (e.g. `BeginLoad`'s
  `Application.CanStreamedLevelBeLoaded` check) used to raise the failure signal within that SAME synchronous
  call stack — before an awaiting node placed right after the load node had even been entered/parked.
  The signal fired into a runner that wasn't listening yet, and only `BaseNodeData.ResumeIfSignalAlreadyRaised`
  could recover it from the context's raised-signal history. `AsyncSceneLoader` now defers the failure branch
  by one frame (`yield return null` before raising), giving the synchronous auto-advance chain time to reach
  and park the awaiting node first — the signal now delivers live in the common case, with
  `ResumeIfSignalAlreadyRaised` remaining useful only as a backup for manually-advanced flows.

### Added
- **`AsyncSceneLoader.StuckOperationWarningAfter` / `OperationTakingTooLong`.** A load/unload that never
  resolves to success OR failure at all (a genuinely hung request) was invisible beyond the graph parking on
  its await signal — no error, no signal, nothing. Logs a `[GraphGameFlow]` warning (and raises
  `OperationTakingTooLong`, scene name + elapsed real seconds) if a single operation has been in flight
  longer than the configurable threshold (default 15s; 0 or less disables it). Purely diagnostic — never
  cancels or alters the flow. Deliberately scoped to the LOADER's own in-flight duration, not to how long a
  graph node has been parked on a signal in general: the latter is routinely minutes for a perfectly normal
  "await player input" node, so a driver-wide timeout on every `WaitingForSignal` state would false-positive
  constantly on the ecosystem's most common await-signal use case.
- **`SceneAwaitSetup.ConfigureLoadAwait(node, completedSignal, failedSignal, resumeIfAlreadyRaised = true)`.**
  Collapses the three separate, easy-to-forget settings a load-await node needs
  (`AwaitSignalName`, `AwaitSignalNamesExtra`, `ResumeIfSignalAlreadyRaised`) into one call for code-first
  graph authors.

## [0.14.0]

### Added
- **`AsyncSceneLoader.LoadFailedSignal`/`UnloadFailedSignal` — a failed load/unload no longer stalls the
  flow silently.** Until now, a load/unload that failed (bad name, not in Build Settings, unloading the
  last scene…) only logged a `[GraphGameFlow]` error — it never raised `LoadCompletedSignal`/
  `UnloadCompletedSignal`, so a flow parked on an await-signal node waiting for that signal stayed parked
  **forever**, with no timeout and no way out. `AsyncSceneLoader` now raises new
  `SceneLoadFailed`/`SceneUnloadFailed` C# events (scene name, then a human-readable reason) and, if
  `LoadFailedSignal`/`UnloadFailedSignal` are set, the matching signal into the target driver with a
  `"{sceneName}: {reason}"` string payload — naming both what failed and why in one glance. Add the failure
  signal as a SECOND name on the same await-signal node (graphcore's `AwaitSignalNamesExtra` — a logical OR
  over several names already supported by the runner) alongside the completion signal, and a failure now
  resumes the flow exactly like a success does, instead of stalling it. No core (graphcore) change needed —
  the multi-signal await already existed.

## [0.13.1]

### Fixed
- **`LoadSceneAction.SetActiveOnLoad` silently never fired for a key-based loader (Addressables).**
  `RequestSetActiveWhenLoaded` used to match the loaded `Scene.name` against `LoadSceneAction.SceneName` —
  correct for a Build-Settings loader (the two are identical there), but `SceneName` is really "whatever
  identifier the `ISceneLoader` in use expects"; for `com.faolline.graphgameflow.addressables`'
  `AddressablesSceneLoader` that's the Addressable KEY (e.g. `"AddrTest.Overlay"`), almost never the same
  string as the resulting scene's own name (`"Overlay"`). The comparison then never matched and activation
  never fired — no error, no warning, just silence. Found via independent testing against the real
  `LoadSceneAction`/`AddressablesSceneLoader` consumer path (this package's own suite never exercised that
  combination together). Fixed loader-agnostically: the handler no longer compares names at all — it claims
  the very next scene-load event unconditionally (unsubscribing whether or not it turns out Additive),
  bounding its lifetime to exactly one scene load instead of matching-by-name or leaking forever. Two
  `SetActiveOnLoad` requests racing in the very same frame for two different scenes can still resolve out
  of order — narrow, and no worse than the previous name-matching behavior's own edge cases.

## [0.13.0]

### Added
- **`GameFlowContext.LoadedScenes` / `IsSceneLoaded`: a loaded-scene registry, kept in sync automatically.**
  Until now the context carried the scene loader service but nothing about which scenes are actually
  loaded — a custom `BaseCondition`/`BaseAction` that needed that had to import `UnityEngine.SceneManagement`
  directly, breaking the "graph logic reads/writes the context" model. `GraphFlowDriver` now subscribes to
  Unity's own `SceneManager.sceneLoaded`/`sceneUnloaded` (not the `ISceneLoader` in use — accurate whether a
  scene loaded through `UnitySceneLoader`, `AsyncSceneLoader`, `AddressablesSceneLoader`, or code entirely
  outside the flow) and mirrors it onto the context via `MarkSceneLoaded`/`MarkSceneUnloaded`, seeding
  whatever is already loaded at `Boot()` time. The subscription is scoped to the driver's own
  `Subscribe()`/`Unsubscribe()` pair (paired with `Stop()`/`OnDestroy`), not to the context's lifetime,
  since `GameFlowContext` has no dispose hook to unhook a static event from.

### Changed
- **`GraphFlowDriver.Active` now documents itself as a deliberate, narrow exception** to this ecosystem's
  "no singletons, no service locator" rule (see `INTEGRATION.md`) rather than leaving that tension implicit
  — it exists only for scene scripts with no wiring path to the driver at all (a physics trigger, a UI
  button). No behavior change; XML doc + `INTEGRATION.md` now say explicitly to prefer an explicit
  reference (a DI-registered driver, or a loader's own `SignalDriver`) wherever one is threadable.

## [0.12.0]

### Added
- **`ISceneUnloader` + `UnloadSceneAction`: the other half of an additive scene flow.** `LoadSceneAction`
  could stack scenes additively but nothing could ever remove one — a hub + streamed-zones/overlay system
  was one-way. `ISceneUnloader` is a separate companion seam (NOT a new member on `ISceneLoader`, so every
  existing loader implementation keeps compiling); `UnitySceneLoader` and `AsyncSceneLoader` implement it
  via `SceneManager.UnloadSceneAsync` (the only non-deprecated unload API), guarded gracefully on "scene
  not loaded" and "last remaining scene" (Unity cannot unload it). `UnloadSceneAction` is the matching
  graphcore `BaseAction` (attachable to any node's enter/exit list, like its load counterpart) with a
  scene-dropdown inspector; a context loader without unload support warns and falls back to the default.
- **`AsyncSceneLoader` completion signals — a flow can now WAIT for its scenes with zero manual wiring.**
  `LoadSceneAction` is fire-and-forget: the flow runs ahead of an async load unless the consumer hand-wires
  `SceneLoadCompleted` into `driver.RaiseSignal`. New optional `LoadCompletedSignal` /
  `UnloadCompletedSignal` (`SignalDef`) + `SignalDriver` (falls back to `GraphFlowDriver.Active`) raise each
  completion into the driver (scene name as string payload) through the resume-and-drain path — park the
  graph on an await-signal node right after the load/unload action and it resumes exactly when the
  operation lands. New `SceneUnloadStarted`/`SceneUnloadCompleted` events and a `PendingCount` property.
- **`LoadSceneAction.SetActiveOnLoad` — an additive scene can take over as the ACTIVE scene.** Unity keeps
  the previous active scene on an additive load (its lighting/fog settings keep applying; new objects parent
  into it) and there was no seam to change that. The new flag (Additive only — Single is activated by Unity
  itself; the inspector shows it only for Additive) registers a one-shot `SceneManager.sceneLoaded` handler
  that calls `SetActiveScene` once the scene has actually finished loading — loader-agnostic, so it works
  with a consumer-written `ISceneLoader` too. Unloading the active scene falls back to a remaining scene,
  Unity-standard.
- **`GraphFlowDriver.Paused` + `AsyncSceneLoader.PauseDriverWhileLoading` — timed waits no longer have to
  tick down behind a loading screen.** `Paused` gates the driver's time pump (`Tick`/`Update` become
  no-ops: a parked timed wait holds its `WaitRemaining`) while deliberate calls stay live — `Advance`,
  `ChooseById`, `RaiseSignal` (so a completion signal raised mid-pause resumes a parked await as usual).
  The loader's opt-in `PauseDriverWhileLoading` manages it automatically: the target driver
  (`SignalDriver`, else `Active`) is paused synchronously with the first queued request and resumed when
  the queue drains; a driver the consumer already paused is left untouched, and the loader's `OnDestroy`
  releases a pause it owns.

### Fixed
- **`AsyncSceneLoader` queues concurrent requests instead of DROPPING them.** A `LoadScene` issued while
  another load was in flight was ignored with a warning — so a graph chaining two scene operations in one
  auto-advance pass (e.g. two additive zone loads back-to-back) silently lost the second and could soft-lock
  a flow gated on its completion. Requests (loads and unloads) now enter a FIFO queue drained serially by
  one pump coroutine; `IsLoading` stays true until the whole queue drains. Event contract per load is
  unchanged (started → progress → ready → completed, activation gate included). Covered by a PlayMode
  regression (`BackToBackLoads_AreQueued_NotDropped`) plus a full additive end-to-end
  (`AdditiveSceneFlowTests`: hub Single-load → overlay Additive-load → overlay unload, gated only by
  completion signals).

## [0.11.0]

### Added
- **`AsyncSceneLoader`: an `ISceneLoader` drop-in that loads through `SceneManager.LoadSceneAsync` and raises
  progress/lifecycle events, for consumers that want a loading screen.** `UnitySceneLoader` (the default) is a
  blocking `SceneManager.LoadScene` with no progress reporting — fine for instant transitions, a hard stall
  for anything heavier. `AsyncSceneLoader` is a `MonoBehaviour` alternative: assign it to
  `GraphFlowDriver.SceneLoader` (or `GameFlowContext.SceneLoader` directly) in place of the default; no
  change to `LoadSceneAction`, which only depends on `ISceneLoader`. It exposes `SceneLoadStarted`,
  `SceneLoadProgress(sceneName, 0..1)`, `SceneLoadReady`, and `SceneLoadCompleted` events plus an
  `AutoActivate` gate — set it false and the scene sits ready-but-inactive until the consumer calls
  `ActivateReadyScene()` (e.g. after a loading-screen fade-out or a "press to continue" beat), and a
  `MinimumDisplayDuration` so a fast load doesn't flash the screen for one frame. The lib ships no visuals —
  the UI stays consumer territory, this is only the mechanism. Persists across the load
  (`DontDestroyOnLoad`) by default so a Single-mode transition doesn't kill its own coroutine mid-load.

## [0.10.0]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.35.0`, covering both the parameter→variable identity re-base
  (spec `033`, graphcore 0.34.0) and the subsequent identity-vocabulary rename (`SignalName`→`SignalDef`,
  `ParameterName`→`VariableDef`, etc., graphcore 0.35.0). No gameflow-specific behaviour change —
  `ContextTrigger` and the driver internals were updated only to reference the renamed graphcore types/API.

## [0.9.0]

### Fixed
- **Auto-advance is now iterative, not recursive — a cycle with no pause node can no longer crash the
  editor/player.** `HandleNodeCompleted` used to call `BaseRunner.Proceed()` directly from inside the
  runner's own `OnNodeCompleted` event, so every pass-through node added a frame to the native call stack
  (`Proceed → EnterCurrentNode → OnNodeCompleted → HandleNodeCompleted → Proceed → …`). Graphgameflow
  explicitly supports cyclic "game-shell" flows (0.4.0, bounded by `HistoryDepth`) — but that pattern only
  works because the loop has an await/wait somewhere; a cycle authored WITHOUT one recursed until the
  native call stack overflowed: an uncatchable `StackOverflowException`, not a recoverable exception.
  `HandleNodeCompleted` now only sets a pending flag; a new `DrainAutoAdvance()` iteratively pumps it (flat
  call stack, any chain length) from every top-level entry point (`Boot`, `Boot(snapshot)`, `Advance`,
  `ChooseById`, `RaiseSignal`, `Tick`). A genuine pause-free cycle now stops after 1000 auto-advanced steps
  with a `[GraphGameFlow]` warning instead of crashing — mirrors `DialoguePlayer.MaxDrainSteps`. Observable
  behavior for every existing flow is unchanged: `OnNodeCompleted`/`OnNodeEntered` still fire once per node,
  in the same order, whether reached through the loop or the old recursion.

## [0.8.2]

### Changed
- **Editor assembly is now `autoReferenced`** — a consumer editor script without its own asmdef can call the
  editor tooling directly (previously reachable only via reflection). No code change.

## [0.8.1]

### Fixed
- **Declared the `com.faolline.graphsave` dependency (≥ 0.5.0).** The Runtime assembly has always referenced
  `com.faolline.graphsave.Runtime` (the `GraphFlowDriver.Boot(GraphRunSnapshot, …)` restore path), but the
  manifest never declared it — installing graphgameflow without graphsave failed to compile. The 0.5.0 floor
  is the first graphsave that restores the raised-signal history, which the load-game path relies on.
- **Editor live-cursor probe detached on `Stop()` and re-`Boot()`.** The driver now calls
  `BaseRunner.DetachEditorProbe()` (graphcore 0.27.0) when it stops or replaces its runner, so a dead run no
  longer shadows the next run's cursor in the graph editor. Dependency floor `com.faolline.graphcore`
  `0.26.0` → `0.27.0`.

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
