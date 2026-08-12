# Changelog

All notable changes to **com.faolline.graphsave** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.10.0]

### Changed
- **Migrated to the shared `com.faolline.graphlogging` facade.** `GraphRunSnapshot`/
  `JsonFileGraphSaveStore` now log through `Logging.*` under `GraphSave`, toggleable from
  `Faolline ▸ Diagnostics ▸ Log Settings`. New dependency: `com.faolline.graphlogging` (0.1.1).

## [0.9.0]

### Added — `IGraphSaveStore.GetAllKeys()` / `DeleteAll()`

Slot discovery and bulk-clear, matching `com.faolline.savesystem.core`'s `ISaveSystem<T>` (which
gained the equivalent `GetAllKeys()`/`DeleteAll()` upstream). Both existing implementations updated:
`JsonFileGraphSaveStore` scans/clears its folder directly; the `com.faolline.graphsave.savesystem`
bridge delegates to the wrapped backend.

**Breaking for any custom `IGraphSaveStore` implementation** — two new required members, added
deliberately rather than via a default-interface-method shim, since no known consumer implements
this interface directly yet outside this repo's own two implementations.

## [0.8.1]

### Fixed
- **Bumped the `com.faolline.graphcore` floor to `0.41.0`.** Stale at `0.38.0` since the 047-graph-soft-links
  merge. Independently notable: this is also the release the new `graphgameflow` `IGraphCatalog` port was
  motivated by (`GraphRunSnapshot.GraphId` → `BaseGraph` resolution for multi-root-graph restore) — no code
  change needed here, `graphgameflow` is the consumer of that seam.

## [0.8.0]

### Changed
- **`JsonFileGraphSaveStore` now REJECTS an invalid slot name instead of sanitizing it.** A slot containing a
  path separator (`/` or `\`) or any OS-reserved filename character used to have those characters silently
  replaced with `_` and still succeed. It now refuses the slot entirely: `Save` logs a `[GraphSave]` error and
  does not persist anything; `Load`/`Exists`/`Delete` treat it as absent, exactly as if the slot didn't exist.
  This aligns with `com.faolline.savesystem.core`'s `JsonSaveSystem`, whose own fix for the same
  path-traversal risk (external repo, commit `330c049`) chose reject-and-log over sanitize-and-succeed —
  the two `IGraphSaveStore` implementations previously diverged on the exact same input, a real trap for a
  consumer switching between them. A legitimate save-name should be constrained by the consumer's own input
  field, not silently rewritten here. The length-bounding (truncate + stable-hash suffix) for an otherwise
  valid but over-long slot name, added in 0.7.1, is unaffected — this is purely about characters/separators.
- **Breaking for any consumer relying on the old sanitize-and-succeed behavior** for a slot name containing a
  path separator or reserved character — such a slot now fails to save. Not expected to affect real usage:
  slot names are normally program-chosen constants or validated save-name fields, not raw untrusted paths.

## [0.7.1]

### Fixed
- **`JsonFileGraphSaveStore.Load` no longer throws on a corrupted/truncated save file.** A crash mid-write (or
  any hand-edited/malformed JSON) made `JsonUtility.FromJson` throw uncaught. `Load` now catches any read/parse
  exception, logs a `[GraphSave]` warning naming the slot and the exception, and returns `null` — the same
  "absent" contract already used for a missing file.
- **`JsonFileGraphSaveStore.Save`/`SlotPath` no longer throws on a long or unicode-heavy slot name.** The
  sanitizer only replaced invalid characters, with no bound on the resulting file name's length, so a slot name
  long enough (or unicode-dense enough, since surrogate pairs count as two UTF-16 units) to push the full path
  past Windows' ~260-char `MAX_PATH` crashed `Directory.CreateDirectory`/`File.WriteAllText`. `SlotPath` now
  budgets the sanitized name's length off the actual `RootPath` (so it adapts to the real
  `persistentDataPath`), truncating (never mid-surrogate-pair) and appending a short deterministic hash when a
  slot name would exceed it — so two different over-length slots never silently collide onto the same file, and
  the truncated form is stable across app restarts (a plain `string.GetHashCode()` is not, since .NET may
  randomize it per process). `Save` also now catches and logs any I/O exception instead of throwing.
- Found via an external stress test (`GraphSaveBridgeTest`) against a fresh isolated project.

## [0.7.0]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.35.0`, covering both the parameter→variable identity re-base
  (spec `033`, graphcore 0.34.0) and the identity-vocabulary rename (`SignalName`→`SignalDef`,
  `ParameterName`→`VariableDef`, `GetAllParameters`→`GetAllVariables`, etc., graphcore 0.35.0). No save-format
  change and no graphsave-specific behaviour change — `GraphRunSnapshot` reads `BaseContext`'s renamed
  `GetAllVariables()`, same values, same JSON shape.

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
