# Changelog

All notable changes to **com.faolline.graphTest** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.3.1]

### Fixed
- **README and package description brought in line with the package.** They now say it also hosts the
  TestGraph sandbox window, the sample graphs and the ecosystem-wide `DependencyMatrixTests`. The README no
  longer names `TestAction`/`TestCondition`, which do not exist (the doubles are `TestLogAction`,
  `TestSetIntAction`, `TestIntCondition`, …).
- **Recorded the dependency floors** already in `package.json`: `com.faolline.graphcore` 0.44.0,
  `com.faolline.graphstandard` 0.18.0 and `com.faolline.graphlogging` 0.2.0, raised by ecosystem-wide syncs
  without a version bump here.

## [0.3.0]

### Changed
- **Migrated to the shared `com.faolline.graphlogging` facade.** Sample builders, sample
  drivers/conditions/actions, and `TestGraphEditorWindow` now log through `Logging.*` under `GraphTest`,
  toggleable from `Faolline ▸ Diagnostics ▸ Log Settings`. New dependency: `com.faolline.graphlogging`
  (0.1.1).

## [0.2.2]

### Fixed
- **Bumped the `com.faolline.graphcore` floor to `0.41.0`.** Stale at `0.38.0` since the 047-graph-soft-links
  merge (`GraphValidatorExtensionRegistry`, GraphLink soft reference).

## [0.2.1]

### Fixed
- **Bumped the `com.faolline.graphstandard` floor to `0.17.0`.** Stale at `0.12.1` — the largest relative gap
  found during an ecosystem-wide version-drift sweep. No code change here.

Internal verification package — not for distribution; history starts being tracked from 0.1.3.

## [0.2.0]

### Added
- `DependencyMatrixTests` (EditMode, `Tests/EditMode/Architecture/`) — locks the ecosystem's assembly
  dependency matrix: every `com.faolline.graph*` / `starterGraph` asmdef must be declared in the
  allowlist, every declared entry must exist on disk, and no asmdef may gain a reference outside its
  allowed set (tier rules: verticals never reference verticals, external deps only in adapter
  assemblies). The matrix mirrors the repo-root `ARCHITECTURE.md` — update both in the same commit
  when adding an assembly or an edge.

## [0.1.4]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.35.0` (the identity-vocabulary rename: `SignalName`→`SignalDef`,
  `ParameterName`→`VariableDef`, `GetAllParameters`→`GetAllVariables`, etc.). The `Test*` actions/conditions
  deliberately kept their raw-string `ParameterKey` API (graphcore's islands escape hatch) — only the editor
  window, inspector, and sample builders were updated to reference the renamed graphcore types.

## [0.1.3]

### Changed
- Bumped `com.faolline.graphcore` floor to `0.34.0` (the parameter→variable identity re-base, spec `033`).
  Removed the obsolete graph-parameter authoring panel test (`TestNodeInspectorParameterPanelTests`) — the
  panel itself was deleted from `BaseNodeInspectorView` (parameters are now `VariableDef` project assets).
  `AddActionTests` (the only graphTest suite exercising graphcore's governed `AddIntAction`/`AddFloatAction`
  directly, rather than a `Test*` double) migrated to reference `VariableDef`.
