# Changelog

All notable changes to **com.faolline.starterGraph** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

Internal verification package — not for distribution; history starts being tracked from 0.4.0.

## [0.6.1]

### Fixed
- **Template fork procedure no longer causes GUID conflicts.** The README now says to duplicate the folder
  inside Unity (Ctrl+D), not in the file explorer, to delete the copied sample asset (it keeps pointing at
  the original package's scripts) and to regenerate it from the renamed sample builder. It also lists the
  `[Icon]` and the sample builder's `Folder` constant among the things to rename.
- `StarterGraph`'s create menu read `StarterGraph/Test Graph`, a leftover from the graphTest origin; it is
  now `StarterGraph/Starter Graph`.
- The README described the package as a "verification package"; it is the template to fork.
- **Recorded the dependency floors** already in `package.json`: `com.faolline.graphcore` 0.44.0 and
  `com.faolline.graphlogging` 0.2.0, raised by ecosystem-wide syncs without a version bump here.

## [0.6.0]

### Changed
- **Migrated to the shared `com.faolline.graphlogging` facade.** `StarterSampleBuilder` and
  `StarterGraphEditorWindow` now log through `Logging.*` under `StarterGraph`, toggleable from
  `Faolline ▸ Diagnostics ▸ Log Settings`. New dependency: `com.faolline.graphlogging` (0.1.1).

## [0.5.1]

### Fixed
- **Bumped the `com.faolline.graphcore` floor to `0.41.0`.** Stale at `0.38.0` since the 047-graph-soft-links
  merge (`GraphValidatorExtensionRegistry`, GraphLink soft reference).

## [0.5.0]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.35.0` (the identity-vocabulary rename: `SignalName`→`SignalDef`,
  `ParameterName`→`VariableDef`, etc.). `StarterSampleGraph.asset` regenerated so its `FlagCond`/`ToggleFlag`
  reference the renamed `VariableDef` type; `StarterRuntimeTests`/`StarterWindowExecutionTests`/
  `StarterEditorTests` migrated accordingly.

## [0.4.0]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.34.0` (the parameter→variable identity re-base, spec `033`).
  The sample's typed `Flag` parameter — previously a per-graph `ParameterData` declaration — is now a
  `VariableDef` sub-asset referenced by `FlagCond`/`ToggleFlag`, seeded declaration-free via
  `BaseContext.InitFromGraph`'s reference scan.
