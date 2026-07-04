# Changelog

All notable changes to **com.faolline.graphlocalization** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.6.1]

### Fixed
- **Multi-line CSV values no longer corrupt the round-trip.** `CsvLocalizationExporter.Escape` correctly
  quoted fields containing newlines, but both CSV readers (`CsvLocalizationProvider` and the exporter's own
  merge-preserve parser) split the text on `\n` BEFORE parsing quotes — so a translation containing a line
  break (multi-line dialogue text, or a translator's spreadsheet cell) broke the row on the next read:
  unresolvable keys at runtime and corrupted/dropped rows on the next rebuild. Both readers now use a
  full-text RFC4180 tokenizer (quoted fields may contain commas, doubled quotes, and newlines), and
  `Escape` also quotes fields containing `\r`.
- **`LocalizationContext` statics reset on Play.** With Enter Play Mode Options (domain reload disabled), the
  ambient settings survived the Edit/Play boundary, so a session reused whatever provider edit-mode tooling
  last left there instead of loading fresh from the settings asset. A
  `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` reset (editor-only) clears it.

## [0.6.0]

### Changed (breaking)
- **Localization flags are now inline on the graph, not a companion asset.** The per-graph
  `GraphLocalizationData` ScriptableObject (one `_Localization.asset` beside every graph) and
  `GraphLocalizationDataUtility` are **removed**. A graph opts into localization by implementing the new
  `ILocalizedGraph` and holding a serialized `GraphLocalizationFlags` field — the same self-contained
  subclass-extension pattern as `DialogueGraph.Speakers`, so graphcore stays localization-agnostic and there
  are no orphan companion files. The inspector section, auto-builder, and per-lib adapters read/write the
  inline flags. Re-set flags in the graph inspector (they persist into the graph asset).

### Added
- **`GraphLocalizationFlags`** (embeddable serializable) + **`ILocalizedGraph`** interface.

## [0.5.0]

### Added
- **One Asset Table per flag type.** The build now generates separate Asset Table collections per
  `LocalizedAssetFlags` type (Text, Audio, Image, …) instead of one catch-all — so the consumer imports only the
  asset types a graph actually uses. Collection names shortened to `GraphName_Text`, `GraphName_Audio`.
- **Per-node `LocalizedAssetMode` / `LocalizedAssetFlags` filtering.** The build respects each node's flags: a
  node that only needs Text won't generate Audio table entries. `LocalizedAssetFlags` is the canonical `[Flags]`
  enum (extracted from graphcore).
- **Auto-create settings + auto-rebuild on graph save.** Saving a graph with the localization window open triggers
  a rebuild if settings exist; if no `LocalizationSettingsAsset` exists, one is created automatically.
- **`SetLocale()` on `ILocalizationProvider`.** The provider contract now supports switching locales at runtime
  (previously locale was set at construction only).
- **Dashboard counts quest/objective key types** so quest graphs show meaningful coverage numbers.

### Changed
- **`LocalizationDatabase` is now transient, not a persisted asset.** The database was a `ScriptableObject` saved
  to disk but only consumed by the build step — it is now created on-the-fly during a build and discarded. One
  fewer asset to track. The `UnityLocalizationTableName` field (also unused) was removed.
- **Stale `Metadata` references removed** from `BaseGraphLocalizationAdapter`.
- **Derive Unity collection prefix from `libName`** for consistency across libs.
- **Prevent auto-builder infinite loop** when settings trigger a rebuild that triggers another save.

### Fixed
- **Manifest asset collection naming** aligned with the per-flag-type tables.
- **Asset tables sit next to string table** — no extra subfolder.

## [0.4.0]

### Added
- **Locale catalog for language pickers.** `LocalizationLocaleCatalog.AvailableLocales()` (editor) returns the
  project's configured locale codes for the active mode — the Unity Localization locales (Project Settings ▸
  Localization) in UnityLocalization mode, otherwise the CSV locale columns; never empty (falls back to `en`).
  Backed by a new `UnityLocalizationSyncer.GetAvailableLocaleCodes()` in the gated Unity adapter, reached by the
  same reflection seam the table builder uses (so the core keeps no compile-time dependency on
  com.unity.localization). Lets editor tools offer a real language dropdown instead of a free-text locale code.
  +3 EditMode tests.

## [0.3.0]

### Added
- **Configurable `ResourcesRoot`** on `LocalizationSettingsAsset` (default `Assets/Resources`) — the build
  (`Faolline ▸ Localization ▸ Build All Tables`) now writes the `GraphLocalizationManifest` and per-lib
  `LocalizationDatabase` assets under this root instead of a hardcoded `Assets/Resources`, so a consumer can
  keep them inside their own game folder (e.g. `Assets/MyGame/Resources`). Must be (or be under) a `Resources`
  folder — the runtime loads these by name via `Resources.Load`; the build warns if it is not, and creates the
  folder hierarchy as needed. (CSV output folder was already configurable.)

### Notes
- Additive (MINOR). From round-6 dogfooding (the build forced two infra assets outside the consumer's game
  folder). Round-7 refinement branch.

## [0.2.0]

### Added
- Localized **asset** resolution: `ILocalizedAssetProvider` + `UnityLocalizedAssetProvider` over Unity
  Localization **Asset Tables** (resolve an asset by the same key as the text, per locale).
- Build manifest records Asset Table collections; **Tables To Generate** setting (Text / Asset / Both)
  controls whether the build also produces mirror Asset Tables (`…_Assets`, same keys).
- Each collection is kept in its own subfolder under `Collections/<lib>/<graph>/`.

## [0.1.0]

### Added
- Provider-agnostic localization: `ILocalizationProvider`, `CsvLocalizationProvider` (with `Append`
  to merge several CSVs), `LocalizationContext`, `LocalizationSettings(+Asset)`, strict/validation modes.
- Optional `UnityLocalizationProvider` (gated by `GRAPHLOCALIZATION_UNITY_LOCALIZATION`) that searches
  every collection in the build manifest and falls back across locales to the source text.
- Build pipeline auto-discovered via `TypeCache` (`IGraphLocalizationAdapter`): per-graph CSV files and
  per-graph Unity String Table collections, indexed by a `GraphLocalizationManifest` in `Resources`.
- Localization **Dashboard** window (coverage per lib/locale).

### Fixed
- Runtime now actually constructs the Unity provider (previously dead code behind an `#if`), so
  UnityLocalization mode resolves keys across per-graph collections instead of falling back to `#key`.
