# Changelog

All notable changes to **com.faolline.graphsave** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
