# Changelog

All notable changes to **com.faolline.graphlocalization** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
