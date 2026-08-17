# com.faolline.graphlocalization

**Version**: 0.9.0 — **Unity**: 6000.x — no required dependencies

Provider-agnostic localization for the Faolline graph ecosystem. Resolves localized text at runtime
through a pluggable `ILocalizationProvider`, and builds translation tables from your graphs at edit
time (CSV or Unity Localization String Tables). Other libs (e.g. `com.faolline.graphdialoguesystem`)
resolve their text through this package without taking a hard dependency on any specific backend.

---

## Installation

Use the **module selector** (recommended): install `com.faolline.graphcore`, then
**Window ▸ Faolline ▸ Graph Ecosystem Modules** and tick *Graph Localization*.

Or add it directly — **Package Manager ▸ + ▸ Add package from git URL**:

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphlocalization#master
```

Pin `#master` to a tag (e.g. `#graphlocalization-v0.1.0`) for reproducible installs.

---

## Concepts

| Type | Role |
|------|------|
| `ILocalizationProvider` | Resolves a key → localized string for a locale. |
| `CsvLocalizationProvider` | Self-contained provider parsing a `Key,locale1,locale2,…` CSV. Merge several with `Append`. |
| `UnityLocalizationProvider` | Gated provider over **com.unity.localization** String Tables (compiled only when that package is present). Searches all collections in the manifest and falls back across locales. |
| `LocalizationContext` | Ambient `Current` settings (provider + locale + strict mode). |
| `LocalizationSettingsAsset` | Project-wide config (mode, locales, validation, strict mode); creates the runtime provider. |
| `GraphLocalizationManifest` | Build-time index (in `Resources`) of the collections/CSV files produced per lib, so the runtime provider can resolve keys spread across per-graph files. |
| `LocalizationDatabase` | Per-lib index of keys (built from graphs by adapters). |
| `IGraphLocalizationAdapter` | Implement (parameterless ctor) to feed your graphs' keys into the build — **auto-discovered** via `TypeCache`. |

---

## Build workflow

1. **Faolline ▸ Localization ▸ Build All Tables** — scans every adapter, writes a per-lib
   `LocalizationDatabase`, then exports to the configured backend:
   - **Csv** → one CSV per graph under `Assets/Localization/Csv/<lib>/`
   - **UnityLocalization** → one String Table collection per graph under
     `Assets/Localization/Collections/<lib>/<graph>/` (+ a `_Global` collection)
   - both indexed by `Assets/Resources/GraphLocalizationManifest.asset`
2. **Faolline ▸ Localization ▸ Dashboard** — review coverage per lib/locale.
3. Translate the empty entries, rebuild as graphs change.

### Modes

- **Backend** (`LocalizationSettingsAsset.Mode`): `Csv` (default) or `UnityLocalization`.
- **Build validation** (`LocaleValidationMode`): `Permissive` / `Warn` (default) / `Strict`.
- **Runtime strictness** (`LocalizationStrictMode`): `Permissive` / `Audit` (default) / `Strict`.

### CLI batch import — unattended / CI (UnityLocalization backend, 0.9.0)

`TranslationImportBatch.Run` via `-executeMethod` imports externally-authored translation CSVs (e.g. from
a dialogue tool's export) into the String Table Collections the build already created — it never creates
a collection itself, so importing into one that doesn't exist yet fails loudly instead of skipping silently:

```
Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Faolline.GraphLocalization.Unity.Editor.TranslationImportBatch.Run \
  -dialogueTranslationsDir Assets/Data/Translations \
  -speakersCsv Assets/Data/Translations/speakers.csv
```

`-dialogueTranslationsDir <path>` imports every `*.csv` in the folder into the
`{Sanitize(fileName)}_Text` collection (the same naming convention the graph sync already used to create
it); `-speakersCsv <path>` imports into the fixed `Global_Text` collection. At least one of the two is
required. Exits `0` only if every requested import succeeded. Only compiled when **com.unity.localization**
is installed (`Localization.Unity` sub-assembly).

---

## Runtime usage

```csharp
// Resolve through the ambient context (configured by the settings asset):
string text = LocalizationContext.Resolve("line_intro");

// Or inject a provider explicitly:
var provider = new CsvLocalizationProvider(csvText, "en");
string greeting = provider.Resolve("speaker_npc_mayor", "en");
```

### Asset Tables & per-node filtering

- **One Asset Table per flag type**: localized assets (audio, sprites, etc.) are organized into separate
  tables by flag type, so each asset kind has its own build/resolution pipeline.
- **Per-node `LocalizedAssetFlags` filtering**: nodes declare which asset flags they use; the build and
  runtime resolve only the relevant asset tables for each node, avoiding unnecessary lookups.

### Provider API

- **`SetLocale()` on `ILocalizationProvider`**: change the active locale at runtime without
  reconstructing the provider. Implementations update their resolution and notify subscribers.
- **Auto-create settings + auto-rebuild on graph save**: the first build auto-creates a
  `LocalizationSettingsAsset` if none exists, and saving a graph in the editor triggers an automatic
  table rebuild (toggle: `LocalizationSettingsAsset.AutoBuild`, default **on** — disable it for large
  projects where the rebuild is slow) so translations stay in sync with authoring changes.

---

To extend the build, subclass `BaseGraphLocalizationAdapter<TGraph>` (auto-discovered via `TypeCache`,
parameterless ctor) — it handles the `AssetDatabase` scan for every `TGraph` asset, you only extract keys:

```csharp
public sealed class MyAdapter : BaseGraphLocalizationAdapter<MyGraph>
{
    public override string LibName => "MyLib";

    protected override int ExtractGraphKeys(MyGraph graph, LocalizationGraphEntry entry)
    {
        // walk graph.Nodes, entry.AddKey(...) per key, return the count added
    }
}
```

For keys not tied to a specific graph (e.g. a shared speaker/name table), override
`ExtractGlobalKeys(LocalizationDatabase)` too. Implementing the lower-level `IGraphLocalizationAdapter`
directly is still possible if you need full control over `ScanAndIndex` — every real adapter in the
ecosystem (dialogue, quest) uses the `BaseGraphLocalizationAdapter<TGraph>` path instead.
