# Changelog

All notable changes to **com.faolline.graphsave.savesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.1.9]

### Added
- **`SaveSystemGraphStore.GetAllKeys()` / `DeleteAll()`** — implements the two new
  `IGraphSaveStore` members (graphsave 0.9.0) by delegating to the wrapped backend's own
  `ISaveSystem<T>.GetAllKeys()`/`DeleteAll()`, with the same defensive try/catch-and-log pattern as
  every other method here.

### Changed
- Dependency floor: `com.faolline.graphsave` raised to `0.9.0`. `manifest.json`'s
  `com.faolline.savesystem.core`/`.json` git dependencies are now pinned to an explicit commit
  (`51a609a1`) instead of floating on the default branch — closes a reproducibility gap (there was
  no `#ref` at all before) and is also the first commit upstream that ships `GetAllKeys()`.

## [0.1.8]

### Changed
- Bumped the `com.faolline.graphsave` floor to `0.8.0` (its `JsonFileGraphSaveStore` now REJECTS an invalid
  slot name instead of sanitizing it, closing a documented asymmetry with this bridge — see graphsave's own
  changelog). `CrossStore_TraversalSlotName_AsymmetricBehaviorIsDocumented` was rewritten to
  `CrossStore_TraversalSlotName_BothRejectConsistently`: both `IGraphSaveStore` implementations now reject the
  same traversal-shaped slot name the same way, so the test that used to document the divergence now confirms
  parity instead — a stale "still broken" assertion would have failed the moment the asymmetry was fixed.

## [0.1.7]

### Added
- **Three end-to-end integration tests through the REAL `JsonSaveSystem` backend** (not synthetic doubles),
  following the external `com.faolline.savesystem.core`/`.json` repo's own path-traversal fix (commit
  `330c049`, `JsonSaveSystem.GetPath` now rejects a key containing a separator or escaping its save folder):
  - `Json_Backend_PathTraversalSlot_DoesNotEscapeAndDegradesGracefully` — confirms the external rejection
    reaches callers through this bridge as graceful null/false/no-op, and that no file is ever written
    outside the backend's own `Saves/` folder.
  - `Json_Backend_CorruptedChecksumOnDisk_ExistsAgreesWithLoad` — re-verifies the 0.1.6 `Exists()`/`Load()`
    consistency fix against the backend's REAL on-disk checksum mechanism (a corrupted byte on disk), not
    just the synthetic `InconsistentBackend` double.
  - `CrossStore_TraversalSlotName_AsymmetricBehaviorIsDocumented` — pins down a real, known trap: the same
    traversal-shaped slot name SUCCEEDS (sanitized) through `JsonFileGraphSaveStore` but silently NO-OPS
    (rejected) through this bridge + `JsonSaveSystem`. Same `IGraphSaveStore` contract, different backend,
    different actual persistence outcome — this test catches it immediately if either implementation's
    strategy ever changes.
- No production code changed; this project's own resolved `com.faolline.savesystem.core`/`.json` git
  packages were also refreshed locally to pick up the external fix (their declared `package.json` version
  stayed `1.0.0` — that repo does not bump semver per fix, only the git content changed).

## [0.1.6]

### Fixed
- **`Exists()` no longer disagrees with `Load()`.** Some backends (e.g. `JsonSaveSystem`) validate integrity
  (a checksum) inside `Load()` but not inside their own `Exists()`, which is a raw presence check — a
  corrupted-but-present file could make `Exists()` report `true` while `Load()` correctly (and gracefully)
  returned `null` for that same slot. `SaveSystemGraphStore.Exists()` now cross-checks against the backend's
  `Load()` result, so it never reports `true` for a slot `Load()` would refuse. `Load()` itself now checks the
  raw backend `Exists()` directly (rather than through the public, now-heavier `Exists()`) so the common
  `if (Exists) Load()` pattern doesn't pay for the validation twice.
- The delegation-without-cross-checking gap was in this bridge's own code, even though the underlying
  checksum mechanism that exposes it lives in the external `com.faolline.savesystem.core` backend.

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
