# Faolline GraphLogging

**Version**: 0.1.1 — **Unity**: 6000.x — **Depends on**: nothing

Shared, category-based logging control for the Faolline graph ecosystem. Any package routes its
`Debug.Log`/`Debug.LogWarning` through `Logging` instead of calling Unity's console API directly, and the
user gets one project-wide settings asset to silence noisy categories — without editing lib source.

---

## Installation

See [`../INSTALL.md`](../INSTALL.md) for the full install guide (module selector or manual git URL).

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphlogging#master
```

## What it gives you

- **`Logging.Info` / `Logging.Warning` / `Logging.Error`** — drop-in replacements for
  `Debug.Log`/`Debug.LogWarning`/`Debug.LogError`, each taking a `category` string first:
  ```csharp
  using Faolline.GraphLogging;

  Logging.Info("GraphCore.Context", "Signal raised: DoorUnlocked");
  Logging.Warning("GraphDialogue.Driver", "No speaker set for line 12", this);   // context object, click-to-ping
  Logging.Error("GraphSave.Store", "Failed to write slot 'autosave'");
  ```
  The trailing `UnityEngine.Object context` parameter is optional (default `null`) and mirrors
  `Debug.Log(message, context)` — pass `this` from a `MonoBehaviour` to keep click-to-ping working in the
  console.
- **`GraphLoggingSettings`** — a project-wide `ScriptableObject`, loaded from `Resources`, with a per-category
  Info/Warning toggle. **`Error` is never gated** — a real problem must always be visible, matching every
  other "fail loud" precedent in this ecosystem. No settings asset, or an unknown category within it, means
  "log everything": adopting this facade never silently loses a message that used to show.
- **Self-registering categories** — a category appears in the settings inspector the first time it logs
  (`EnsureCategoryKnown`, Editor-only). No upfront registry to maintain: as packages adopt `Logging`, their
  categories just show up.

## Settings UI

**Faolline ▸ Diagnostics ▸ Log Settings** creates (if missing) and selects the settings asset at
`Assets/Resources/GraphLoggingSettings.asset`. Categories are grouped by their prefix before the first `.`
(e.g. every `"GraphCore.*"` category groups under **GraphCore**), each group foldable with its own
tri-state master toggle (on/off when every entry in the group agrees, mixed otherwise) — flip a whole
package's logging on or off in one click, or drill into a single category.

## Category naming convention

Categories follow `"<Package>.<Area>"`, e.g. `"GraphCore.Context"`, `"GraphDialogue.Driver"`,
`"GraphSave.Store"` — the prefix before `.` is what the settings UI groups on, so keeping to this shape is
what makes the per-package master toggle useful. A category with no `.` groups under its own full name.

## Adopting this in your own package

1. Add `com.faolline.graphlogging` as a dependency (any tier can — it has none of its own).
2. Replace `Debug.Log`/`LogWarning`/`LogError` call sites with `Logging.Info`/`Warning`/`Error`, picking a
   `"<YourPackage>.<Area>"` category per call site (or per class).
3. Nothing else to register — the category appears in the settings asset the first time it actually logs.

## Architecture

```
com.faolline.graphlogging/
  Runtime/
    Logging.cs                    ← the Info/Warning/Error facade
    GraphLoggingSettings.cs       ← project-wide settings asset (per-category Info/Warning toggles)
    GraphLoggingSettingsLoader.cs ← Resources.Load wrapper + default asset path
  Editor/
    GraphLoggingMenu.cs           ← Faolline ▸ Diagnostics ▸ Log Settings (create-or-select)
    GraphLoggingSettingsEditor.cs ← grouped, foldable inspector with tri-state master toggles
```

## Layering

A T0 foundation leaf with **zero dependencies** — see [`../ARCHITECTURE.md`](../ARCHITECTURE.md). Both
`graphcore.Runtime` and `graphlocalization` depend on it directly for exactly this reason: `graphlocalization`
is itself a near-zero-dependency T0 package and could not otherwise share a logging facility with `graphcore`
without breaking that invariant. Any other package, at any tier, can adopt it the same way without pulling in
`graphcore`.
