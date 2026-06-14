# Changelog

All notable changes to **com.faolline.graphlocalization** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
