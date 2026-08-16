# Changelog

All notable changes to **com.faolline.graphimport** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

Reconstructed retroactively from git history (this file did not exist before 0.5.0) — entries before 0.5.0
describe what actually shipped in each tagged version, including the quest/flow pipeline later removed.

## [0.5.0]

### Removed
- **Quest/flow generation dropped — dialogue-only import pipeline.** The quest/flow half (design issues
  need reworking) is stripped out: `Pivot`/`Branching`/`Mapping`/`Sources`/table-based resolution,
  `QuestAssetGenerator`, `FlowAssetGenerator`, `GraphImportBatch`, `CombinedImportBatch`,
  `GraphImportPipeline`, and the `CryptiqueExample` sample. No consumer used this part of the lib, so no
  migration was needed. The full implementation is preserved on branch `archive/graphimport-quest-flow`.
  Dialogue/speaker generation (+ localization via `ILocalizedGraph`) keeps working unchanged — it's now
  the package's only responsibility.

## [0.4.1]

### Fixed
- `FlowAssetGenerator` titled every SubGraph node with the raw pivot step id (`"S_001"`) instead of a
  legible name. Now resolves the title through the same content-table join already used for dialogue path
  tokens and Speaker folder routing — falls back to the step id when no content-fields lookup is given, the
  content isn't in it, or its table declares no `"name"` field. Wired through all three entry points that
  built a `FlowAssetGenerator` (`CombinedImportBatch`, `GraphImportBatch`, `GraphImportWindow`) — the latter
  two never built a content-fields lookup before this, which also fixed `GraphImportWindow`'s dialogue path
  template silently never joining against a content table.

## [0.4.0]

### Added
- Adopted `com.faolline.graphlogging`: every console call now routes through `Logging.Info/Warning/Error`
  under category `"GraphImport"`, toggleable from **Faolline ▸ Diagnostics ▸ Log Settings** instead of
  hard-coded `Debug.Log`.

## [0.3.6]

### Added
- `ProjectAssetResolver`'s created Speaker folders can now resolve through the same content-table join as
  dialogue path tokens — an optional `speakerFolderTemplate` + a `contentFieldsById` lookup keyed by speaker
  key (e.g. `"Content/{chapter}/Graph/Speakers"`) instead of the fixed `speakerFolder`. Unknown/unjoined
  tokens throw rather than silently falling back, matching the pipeline's never-guess precedent. Wired
  through `CombinedImportBatch` via a new `-speakerFolderTemplate` flag; `speakerFolder` stays the default
  when no template is given.

## [0.3.5]

### Added
- Dialogue path templates can resolve extra tokens (e.g. `{chapter}`) against a content-role table
  (`TableRole.Content`), alongside the existing `{name}`/`{id}`. `CombinedImportBatch` wires the join since
  it already loads both the quest/flow mapping and the dialogue interchange JSON together.
  `DialogueImportBatch` (dialogue-only, no mapping) is unaffected — still `{name}`/`{id}` only.

## [0.3.4]

### Fixed
- Test-only: folder-scoped Speaker search fix. No runtime behavior change.

## [0.3.3]

### Fixed
- Test-only: sample-test path resolution — a dev-repo-only `Application.dataPath` assumption that broke
  for any real consumer install. No runtime behavior change.

## [0.3.2]

### Fixed
- `GraphImportWindow`'s quest/flow section, non-functional since 0.3.0. Module selector's graphimport
  version entry synced.

## [0.3.1]

### Fixed
- Stable dialogue node ids, shipped after the 0.3.0 tag.

## [0.3.0]

### Added
- Real `ProjectAssetResolver` (0.2.0 shipped only the null placeholder).
- `GraphImportBatch` / `DialogueImportBatch` / `CombinedImportBatch` — `-executeMethod` CLI entry points
  for unattended/CI runs, plus shared `BatchArgs` `-flag value` parsing.
- Combined quest+dialogue cross-resolution fixes.

## [0.2.0] — dialogue graph generation from a pivot interchange format

Unity-side half of Part 2 of the quest-data-import initiative (spec 049) — the external authoring tool
stayed out of scope; a hand-authored interchange JSON stood in for it.

### Added
- `InterchangeDialogueSet` → `DialoguePivotBuilder` → `PivotDialogue` (Line/Choice/End/SubDialogueLink),
  validated fail-fast (dangling `next`, duplicate node id, bad entry point, cross-dialogue reference cycle)
  before any asset is touched.
- `DialogueAssetGenerator` builds real `DialogueGraph` assets via `DialogueGraphBuilder` — text lands on
  `Title`, so an existing localization table build picks it up with no new loc-specific code.
- `PlanEntryKind.DialogueAsset` + `PlanBuilder.BuildDialogues`, reusing the existing Plan/Apply/
  ConflictReport pipeline unchanged — a dialogue collision is reported exactly like a quest/flow one.
- Shared `IProjectAssetResolver` seam (`NullProjectAssetResolver` for V1, matching the existing
  null-`TargetGraph` precedent), retrofitted onto `FlowAssetGenerator` so both generators' asset lookup
  went through one seam.
- Registered in the ecosystem's `DependencyMatrixTests` for the first time (was silently uncovered since
  0.1.0); added the T4 "generation tooling" tier to `ARCHITECTURE.md` — the one sanctioned exception to
  "verticals never reference verticals," since graphimport authors assets via public builder APIs and
  never executes a graph.

## [0.1.0] — quest/flow graph generation from structured data

### Added
- Initial release. Declarative per-table mapping (CSV/JSON, ID-or-name reference resolution, never
  guesses) → internal pivot model (quest/step/branch/reference) → a pure plan-then-apply pipeline.
  Conflicts on apply are only ever reported, never silently overwritten nor silently skipped, for both
  headless/CI and Editor-review use. Branch detection explicit (declared outcome column only, never
  inferred from text). Generated real `graphquest`/`graphgameflow` assets, referencing step content via
  `SubGraphNodeData`.
- *(This quest/flow pipeline was removed in 0.5.0 — see above.)*
