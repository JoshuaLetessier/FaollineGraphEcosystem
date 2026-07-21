# Changelog

All notable changes to **com.faolline.graphgameflow.addressables** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
