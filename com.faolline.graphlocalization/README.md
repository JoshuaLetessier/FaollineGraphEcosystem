# com.faolline.graphlocalization

**Version**: 0.1.0 — **Unity**: 6000.x — no required dependencies

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
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphlocalization#master
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

---

## Runtime usage

```csharp
// Resolve through the ambient context (configured by the settings asset):
string text = LocalizationContext.Resolve("line_intro");

// Or inject a provider explicitly:
var provider = new CsvLocalizationProvider(csvText, "en");
string greeting = provider.Resolve("speaker_npc_mayor", "en");
```

To extend the build, implement `IGraphLocalizationAdapter` (auto-discovered):

```csharp
public sealed class MyAdapter : IGraphLocalizationAdapter
{
    public string LibName => "MyLib";
    public void ScanAndIndex(LocalizationDatabase db) { /* add keys from your assets */ }
}
```
