# Changelog

All notable changes to **com.faolline.graphTest** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

Internal verification package — not for distribution; history starts being tracked from 0.1.3.

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
