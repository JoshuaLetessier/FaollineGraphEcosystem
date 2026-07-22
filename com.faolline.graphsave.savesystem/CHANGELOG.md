# Changelog

All notable changes to **com.faolline.graphsave.savesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.1.5]

### Fixed
- **`SaveSystemGraphStore` no longer lets a throwing backend crash the caller.** All four methods forwarded
  directly to the wrapped `ISaveSystem<T>` with no exception handling — a less-defensive custom backend (or a
  transient I/O failure inside one) propagated raw. `Save`/`Delete` now catch and log a `[GraphSave]` error/
  warning instead of throwing; `Load`/`Exists` catch, log, and degrade to `null`/`false` (the same "absent"
  contract `IGraphSaveStore.Load` already documents for a missing slot). `Load` now calls the bridge's own
  (now exception-safe) `Exists` instead of the raw backend, so a throwing `Exists` short-circuits to `null`
  without a second call into the backend.
- Bumped the `com.faolline.graphsave` floor to `0.7.1` (the sibling `JsonFileGraphSaveStore` hardening fix).
- Found via an external stress test (`GraphSaveBridgeTest`) against a fresh isolated project.

## [0.1.4]

### Fixed
- **Bumped the `com.faolline.graphsave` floor to `0.7.0`.** Stale at `0.3.2` — four minor releases behind.
  Found during an ecosystem-wide version-drift sweep (this was the largest gap found). No code change here.

## [0.1.3]

### Changed
- **Dependency floor `com.faolline.savesystem.core` `0.0.0` → `1.0.0`** — a real floor instead of the
  accept-anything placeholder. No code change.

## [0.1.2]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphsave` floor `0.3.1` → `0.3.2` to match the
  current ecosystem. No code change.

## [0.1.1]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphsave` floor to `0.3.1` to match the
  current ecosystem. No code change.

## [0.1.0]

### Added
- **`SaveSystemGraphStore`** — an `IGraphSaveStore` (from `com.faolline.graphsave`) backed by a UnitySaveSystem
  backend (`ISaveSystem<GraphRunSnapshot>` from `com.faolline.savesystem.core`). Wrap whichever backend you added
  (e.g. `new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>())`), then save/load `GraphRunSnapshot`s
  by slot through your save backends.

### Notes
- Optional bridge package — add only if you use UnitySaveSystem; `graphsave` core stays dependency-free. The
  bridge depends only on the save-system CORE; pick the concrete backend sub-package (json, playerprefs, …)
  yourself. 2 EditMode tests (backend delegation + a real JSON disk round-trip).
