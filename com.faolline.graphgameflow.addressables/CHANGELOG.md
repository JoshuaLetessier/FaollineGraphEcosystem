# Changelog

All notable changes to **com.faolline.graphgameflow.addressables** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.5.2]

### Fixed — concurrent `Resolve` of the same `graphId` silently leaked a handle

Found in a second dogfooding round against a dedicated Addressables test project: calling
`AddressablesGraphCatalog.Resolve` twice for the same `graphId` before either completed — both
succeeded, but the second completion overwrote the first's stored handle, so one of the two
Addressables-refcounted handles was never reachable by `Release` again. Narrow (only the exact
concurrent-same-key case), not a common usage pattern, but a real leak. `_handles` now maps each
`graphId` to a list of handles; `Release` releases all of them.

## [0.5.1]

### Fixed — two gaps found dogfooding 0.5.0 against a dedicated Addressables test project

- **`PreloadNextChapterAction.SignalDriver`** — new public property (was write-only via the
  serialized field, with no setter). A graph built in code had no way to target a specific driver;
  it always fell back to `GraphFlowDriver.Active`, which depends on `PersistAcrossScenes` having
  been set before `Awake()` — a real ergonomics gap, not the signal-delivery bug it first looked
  like. Mirrors `AddressablesSceneLoader.SignalDriver` exactly.
- **`AddressablesGraphCatalog.Release(graphId)`** — new method. Neither `AddressablesGraphCatalog`
  nor `PreloadNextChapterAction` ever released its Addressables handle after resolving, unlike
  `AddressablesSceneLoader`; a resolved/preloaded graph stayed in memory indefinitely with no way to
  free it. `AddressablesGraphCatalog` now tracks its handles (mirroring `AddressablesSceneLoader`'s
  own loaded-scenes dictionary) and exposes `Release`; `PreloadNextChapterAction` gained
  `ReleaseNextChapter()` (delegates to `AssetReference.ReleaseAsset()`).

## [0.5.0]

### Added — graph-side Addressables adapter (soft chapter preloading)

- **`AddressablesGraphCatalog`** — `IGraphCatalog` (graphgameflow 0.17.0) resolving a `graphId` to a
  `BaseGraph` via `Addressables.LoadAssetAsync`, mirroring `AddressablesSceneLoader`'s shape.
- **`AddressablesGraphKeyProvider`** — `IGraphKeySourceProvider` mirroring `AddressablesSceneKeyProvider`,
  filtered to `BaseGraph`-typed Addressable entries; self-registers into `GraphKeySourceRegistry`.
- **`PreloadNextChapterAction`** — `BaseAction` carrying a soft `AssetReferenceT<BaseGraph>` (never a
  build-time dependency of the graph that owns it). Triggers an early asynchronous load of the next
  chapter; on completion sets `GameFlowContext.PendingNextGraph` and optionally raises a completion/failure
  `SignalDef`, supporting both the early-trigger-then-reboot and park-on-signal usage forms — no change
  to `BaseRunner`.

### Changed
- Dependency floor: `com.faolline.graphgameflow` raised to `0.17.0` (required for `IGraphCatalog`/
  `GraphKeySourceRegistry`).

## [0.4.0]

### Added
- **`com.faolline.graphgameflow.addressables.Editor`** — new Editor sub-assembly. `AddressablesSceneKeyProvider`
  registers with `graphgameflow`'s new `SceneKeySourceRegistry` seam (`graphgameflow` 0.16.0) so registered
  Addressable scene addresses show up as a dropdown on the `LoadSceneAction`/`UnloadSceneAction` inspectors,
  alongside a "Mark as Addressable" button to promote a plain project scene in one click. `graphgameflow`
  core stays external-dependency-free — this adapter package owns the only reference to
  `UnityEditor.AddressableAssets`, per the ecosystem's port/adapter rule (see `ARCHITECTURE.md`).

### Fixed
- **Bumped the `com.faolline.graphgameflow` floor to `0.16.0`** (needed for `SceneKeySourceRegistry`).

## [0.3.2]

### Fixed
- **Bumped the `com.faolline.graphgameflow` floor to `0.15.1`.** Stale at `0.15.0` — one patch behind. Found
  while fixing graphsave's corrupt-file/long-slot-name hardening. No code change here.

## [0.3.1]

### Fixed
- **Bumped the `com.faolline.graphgameflow` floor to `0.15.0`.** Stale at `0.13.1` — two floor-worthy core
  releases behind (0.14.0's `LoadedScenes` registry, 0.15.0's instant-failure signal-ordering fix this very
  package's own `AddressablesSceneLoader` relies on). Found during an ecosystem-wide version-drift sweep; the
  same category of bug as the earlier `0.12.0` floor fix, recurring because a floor isn't automatically kept
  in sync when the depended-on package moves — see the ecosystem audit notes for the general pattern. No
  code change here.

## [0.3.0]

### Fixed
- **An Addressables load that fails INSTANTLY (invalid/unregistered key) could raise `LoadFailedSignal` too
  early to be caught live.** Mirrors `AsyncSceneLoader` 0.15.0's fix: `handle.Status != Succeeded` can be
  known within the same synchronous call stack as `LoadSceneAction.Execute()`, before an awaiting node
  placed right after the load node had even parked. `LoadRoutine`/`UnloadRoutine` now defer their failure
  branch by one frame, so the signal delivers live in the common case without needing
  `BaseNodeData.ResumeIfSignalAlreadyRaised` as the only recovery path.

### Added
- **`AddressablesSceneLoader.StuckOperationWarningAfter` / `OperationTakingTooLong`** — same diagnostic as
  `AsyncSceneLoader` 0.15.0: logs a warning (and raises an event) if a single load/unload has been in flight
  longer than a configurable threshold (default 15s; 0 disables it), without ever altering the flow.

## [0.2.0]

### Added
- **`AddressablesSceneLoader.LoadFailedSignal`/`UnloadFailedSignal`.** Found via independent testing on a
  fresh isolated project (see this package's own dogfood notes): a load that fails (invalid key, bundle
  missing from a real packed build…) never raised `LoadCompletedSignal`, so a flow parked on an await-signal
  node waiting for it stalled forever with no escape route. Mirrors `AsyncSceneLoader` 0.14.0's fix: new
  `SceneLoadFailed`/`SceneUnloadFailed` C# events (key, then a human-readable reason) and, if
  `LoadFailedSignal`/`UnloadFailedSignal` are set, the matching signal raised with a `"{key}: {reason}"`
  string payload. Add the failure signal as a second `AwaitSignalNamesExtra` entry alongside the completion
  signal on the same await node — a failure resumes the flow instead of stalling it. No graphgameflow core
  change needed (the multi-signal await already existed).

## [0.1.1]

### Fixed
- **Bumped the `com.faolline.graphgameflow` floor to `0.13.1`.** Versions below that still carry the
  `LoadSceneAction.SetActiveOnLoad` bug where activation silently never fired for a key-based loader —
  exactly this package's `AddressablesSceneLoader`. The stale `0.12.0` floor let a consumer install this
  bridge against a core version where `SetActiveOnLoad` never actually worked with it. No code change here;
  the fix lives in `graphgameflow` itself.

## [0.1.0]

### Added
- **`AddressablesSceneLoader`: an `ISceneLoader`/`ISceneUnloader` backed by `com.unity.addressables`.**
  Loads a scene by Addressable key (address/label/GUID) instead of a Build Settings entry — no other change
  needed to `LoadSceneAction`/`UnloadSceneAction`, which only depend on the seam interfaces. Mirrors
  `AsyncSceneLoader`'s full contract: FIFO request queue (never drops a concurrent request), progress +
  lifecycle events (`SceneLoadStarted`/`Progress`/`Ready`/`Completed`, `SceneUnloadStarted`/`Completed`),
  a manual activation gate (`AutoActivate`/`ActivateReadyScene`/`MinimumDisplayDuration`), optional
  `LoadCompletedSignal`/`UnloadCompletedSignal` raised into a `GraphFlowDriver` (key as payload) so a flow
  can await its scene operations with zero manual wiring, and opt-in `PauseDriverWhileLoading`. New package
  (T3 adapter tier, alongside `graphsave.savesystem`) so Addressables is never forced on a consumer of the
  core `graphgameflow` package.
