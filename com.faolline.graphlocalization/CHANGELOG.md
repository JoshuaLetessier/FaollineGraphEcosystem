# Changelog

All notable changes to **com.faolline.graphlocalization** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.9.0]

### Added
- **`TranslationImportBatch`** (`Localization.Unity/Editor/Batch`) — `-executeMethod` entry point that
  imports externally-authored translation CSVs (Dialogue Studio's export format: `Key` + one column per
  locale, RFC4180) into the String Table Collections `UnityLocalizationSyncer` already created for the
  project's graphs. `-dialogueTranslationsDir <path>` imports every `*.csv` in the folder into
  `{fileNameWithoutExtension}_Text` (the same sanitized-name convention the syncer used to create the
  collection); `-speakersCsv <path>` imports into the fixed `Global_Text` collection. At least one of the
  two is required. Never creates a collection itself — an import targeting a collection that doesn't
  exist yet (its graph was never generated/synced) is reported as a failure, not silently skipped nor
  auto-created, matching this ecosystem's "never guess" precedent. Exits 0 only if every requested import
  succeeded.

## [0.8.0]

### Changed
- **Removed low-value auto-fired `Debug.Log` noise.** `LocalizationBuilderCore`'s per-lib "Phase 1"/
  "Mode is CSV, skipping"/"Phase 2 complete" lines, `CsvLocalizationExporter`'s per-lib coverage dump,
  and `UnityLocalizationSyncer`'s "Sync complete" report all fired on every single autobuild (i.e. on
  every graph save when Auto Build On Save is enabled), flooding the console with routine progress
  narration. Genuine problems (translation gaps) were already separately reported via `LogWarning`/
  `LogError` right next to each of these and are unaffected.
- **Every remaining `Debug.Log`/`LogWarning`/`LogError` call now routes through the new
  `com.faolline.graphlogging` package's `Logging.Info/Warning/Error(category, message)`.** Three
  categories: `GraphLocalization.AutoBuild` (the two lines an autosave now prints — "Auto-rebuilding
  tables..." and "Done. N lib(s) processed." — plus one-time asset-creation notices),
  `GraphLocalization.Validation` (every build-time config/coverage-gap warning and error, editor-side),
  `GraphLocalization.Playback` (the two runtime warnings: missing key during playback, Unity Localization
  provider fallback). Each is independently toggleable from `Faolline ▸ Diagnostics ▸ Log Settings` —
  `Error` calls are never gated, matching this package's existing `Strict` validation precedent. New
  dependency: `com.faolline.graphlogging` (0.1.0) — a zero-dependency T0 leaf, chosen specifically so this
  package keeps its own zero-dependency, install-alone status (see `ARCHITECTURE.md`).

### Fixed
- **Orphan Unity Localization collections are no longer silently dropped from the log.** They were only
  ever surfaced inside `UnityLocalizationSyncer`'s removed "Sync complete" report; now reported directly
  as a warning when found, so removing the noisy summary didn't also remove the one genuinely
  actionable piece of information it carried.

## [0.7.2]

### Fixed
- **`UnityLocalizationSyncer.EnsureLibFolder` could fail to create `Assets/Localization/Collections`
  on the very first sync.** When neither `Assets/Localization` nor `.../Collections` existed yet, a
  single conditional `CreateFolder` call created only `Assets/Localization`, never `Collections`
  underneath it in the same pass — the next `CreateFolder(CollectionsRoot, libName)` call then
  failed with Unity's own "Failed to create folder" (its parent didn't exist), self-healing only on
  the following sync. Now creates each folder level explicitly, one at a time.

### Added
- **`Auto Build On Save` is now visible in the Localization Settings inspector.** `LocalizationSettingsAsset.AutoBuild`
  was already read by `LocalizationAutoBuilder` to gate the on-save rebuild, but the custom inspector
  never drew the field — there was no way to toggle it from the Editor.

## [0.7.1]

### Fixed
- **`ScanAndIndex` no longer logs on every call.** The per-adapter `Debug.Log` fired even when
  `ScanAndIndex` was called for read-only purposes, notably `LocalizationDashboardWindow.Refresh()`
  on `OnEnable`/`OnFocus` — which reruns on every domain reload (Play/Stop), spamming the console.
  The real build path (`LocalizationBuilderCore`) already logs its own per-adapter summary, so this
  log was redundant there too.

## [0.7.0]

### Added
- **`Unity Source Locale` setting.** UnityLocalization mode now declares which locale the authored text is
  written in (`LocalizationSettingsAsset.UnitySourceLocale`), like CSV mode's first-locale convention. The
  syncer resolves: explicit setting → Project Locale → first configured locale, and the last fallback now
  WARNS — it is an alphabetical accident that silently filed French authored text under 'en'.
  (Consumer dogfood finding.)

### Fixed
- **`UnityLocalizationProvider` self-initializes and stops failing silently.** Unity Localization loads its
  locales asynchronously; before that, `SetLocale`/`CurrentLocale` silently no-oped — worst in a player
  build, where an early `SetLocale` from a boot script never applied. The provider now blocks once on
  `InitializationOperation` before touching locales, and `SetLocale` warns when the locale is unknown or
  none are available. (Consumer dogfood finding.)

### Changed
- **Editor assemblies are now `autoReferenced`** (main + Localization.Unity.Editor), so consumer editor
  scripts without an asmdef can reach the build/validation entry points.

## [0.6.2]

### Changed
- **Missing-key reaction now belongs to the StrictMode owner, not the provider.**
  `CsvLocalizationProvider.Resolve` no longer logs a warning on a missing key — it returns the `#key`
  marker silently (the marker IS the signal). Previously `Permissive` was never actually silent, the
  warning double-logged next to `DialoguePresenter`'s own Audit warning, and a UI re-resolving per frame
  could spam the console. `LocalizationSettings.Resolve` now applies `StrictMode` itself: Permissive is
  silent, Audit warns once per key+locale, Strict throws `LocalizationException` — the same semantics
  `DialoguePresenter` already had.

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
