# Changelog

All notable changes to **com.faolline.graphsave** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.6.0]

### Added
- **`GraphRunSnapshot` captures and restores collection quantities (graphcore 0.32.0 stacking).**
  `Collection` gains a new `Counts` field, parallel to `Items` — additive, so a save file written before this
  version deserializes it as an empty list, and `ApplyTo` treats an absent/short/non-positive count as
  quantity 1 (the exact pre-0.6.0 behavior). No existing save loses data or changes behavior. `Capture` reads
  quantities via `BaseContext.GetCollectionWithCounts`; `ApplyTo` uses the additive stacking overload for any
  item captured at quantity > 1.
- **Documented caveat**: in merge mode (`ApplyTo(context, replaceCollections: false)`, the non-default), an
  item captured at quantity > 1 is applied via the ADDITIVE overload — re-applying the same snapshot twice
  in merge mode stacks that quantity again each time. Not an issue with `replaceCollections: true` (used by
  `Restore()` and the one real consumer, `GraphFlowDriver`), since each call starts from a cleared collection.

### Changed
- Dependency floor `com.faolline.graphcore` `0.22.0` → `0.32.0` (`GetCollectionWithCounts`).

## [0.5.2]

### Fixed
- **`JsonFileGraphSaveStore` resolves `persistentDataPath` lazily.** The constructor called
  `Application.persistentDataPath`, which Unity forbids during MonoBehaviour construction — so
  `new JsonFileGraphSaveStore()` as a field initializer threw. The path is now resolved on first
  Save/Load/Exists/Delete; the constructor stores only the sub-folder string. (Consumer dogfood finding.)

## [0.5.1]

### Fixed
- **Dependency floor corrected `0.17.0` → `0.22.0`.** `GraphRunSnapshot` has used graphcore's signal-history
  API (`GetAllRaisedSignals` / `RestoreSignalHistory`, introduced in graphcore 0.22.0) since 0.5.0, but the
  manifest still declared the 0.17.0 floor — a consumer resolving graphcore 0.17–0.21 would not compile.
- **`JsonFileGraphSaveStore` sanitizes slot names.** Path separators and invalid filename characters in a
  slot (e.g. `"../other"`, `"a/b"`) are replaced with `_`, so a slot can neither escape the store folder nor
  fail on Windows-invalid characters.

## [0.5.0]

### Added
- **Raised-signal history in the snapshot.** `GraphRunSnapshot` now captures every signal name raised in the
  context (`RaisedSignals`) and restores it on `ApplyTo`/`Restore` via `BaseContext.RestoreSignalHistory`, so
  `HasSignalBeenRaised`-based logic (e.g. `ResumeIfSignalAlreadyRaised` awaits, `SignalRaisedCondition` gates)
  survives a save/load. Requires graphcore ≥ 0.22.0.
  *(Entry added retroactively in 0.5.1 — it was missing when 0.5.0 shipped.)*

## [0.4.0]

### Added
- **`JsonFileGraphSaveStore`** — batteries-included `IGraphSaveStore` backed by JSON files under
  `Application.persistentDataPath`. Each slot is one `.json` file. No dependencies beyond graphcore. For
  production games needing encryption or cloud sync, implement `IGraphSaveStore` directly or use the
  `com.faolline.graphsave.savesystem` bridge.

### Changed
- **Defensive parse warnings on restore.** `GraphRunSnapshot.ApplyParam` now logs a `[GraphSave] Skipping
  param '…'` warning when a value cannot be parsed (corrupted/edited/outdated save), instead of silently
  skipping the key.

## [0.3.2]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphcore` floor `0.14.0` → `0.17.0` to match
  the current ecosystem. No code change.

## [0.3.1]

### Changed
- **Dependency floor alignment (chore).** Bumped the `com.faolline.graphcore` floor to `0.14.0` to match the
  current ecosystem. No code change.

## [0.3.0]

### Added
- **Value-type parameters round-trip.** Following graphcore's new `Vector2` / `Vector3` / `Color` parameter types,
  the snapshot now persists them. They are flattened to a comma-separated, invariant-culture component string in the
  existing `Param.Value` (tags `"vector2"` / `"vector3"` / `"color"`) — exactly like bool/int/float already are — so
  the snapshot stays a plain POCO with NO raw Unity structs. That keeps it round-tripping through both `JsonUtility`
  AND any reflection-based JSON backend (a raw `Vector2`/`Vector3` field would trip Newtonsoft on its self-referencing
  `normalized` property, breaking even unrelated params). `ApplyTo` parses the components back and restores with
  `context.Set<Vector2/Vector3/Color>`.

### Notes
- Additive (MINOR); the on-disk shape is unchanged (same `Param` fields — value types just use the `Value` string).
  +1 EditMode test (a Vector2/Vector3/Color context round-tripped through JSON). Requires graphcore ≥ 0.13.0.

## [0.2.0]

### Changed
- **`ApplyTo` can now replace collections.** New `ApplyTo(context, bool replaceCollections = false)`: when true,
  each captured collection key is cleared before its items are re-added, so the snapshot is authoritative
  (avoids the silent doubling you get applying onto an already-populated context). `Restore` now uses this
  (`replaceCollections: true`). Default stays additive — existing callers are unaffected. From FaollineMiniGame
  dogfooding (the consumer had to `ClearCollection` by hand before `ApplyTo`).

### Notes
- **Top-level only (documented).** A snapshot's `CurrentNodeId` is the TOP frame's node and the stack is not
  captured, so a node saved mid-sub-graph (e.g. mid-dialogue) cannot be restored via `Restore`. Capture/restore
  at top-level checkpoints — pair with `BaseNodeData.IsCheckpoint` nodes (a checkpoint just before a long,
  non-replayable sequence doubles as the save point). `Capture`/`Restore` XML docs now spell this out. Additive
  (MINOR); graphcore untouched. +1 EditMode test.

## [0.1.0]

### Added
- **Neutral save core.** `GraphRunSnapshot` — a serializable snapshot of a running graph: the execution
  context's typed parameters + named collections, plus the current node id (and graph id). `Capture(context, …)`
  / `Capture(runner, context)` take it; `ApplyTo(context)` writes it back; `Restore(runner, graph, context)`
  re-enters the saved node via `BaseRunner.StartFrom`. graphcore exposes everything needed, so this layer adds
  no engine dependency and is fully testable headlessly.
- **`IGraphSaveStore`** — a neutral slot-based persistence seam (`Save`/`Load`/`Exists`/`Delete`). Bring your own
  backend, or add `com.faolline.graphsave.savesystem` to bridge `com.faolline.savesystem.core`. The snapshot is a
  plain serializable object, so you can also skip the store and (de)serialize it yourself.

### Notes
- Optional layer above graphcore; nothing in the ecosystem depends on it. 4 EditMode tests.
