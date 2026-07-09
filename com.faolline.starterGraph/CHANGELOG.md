# Changelog

All notable changes to **com.faolline.starterGraph** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

Internal verification package — not for distribution; history starts being tracked from 0.4.0.

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
