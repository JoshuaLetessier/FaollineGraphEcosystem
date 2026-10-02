# Contract: Public C# API changes

Only additions and optional parameters. No existing member is removed, renamed, or has its signature changed.

## com.faolline.graphlocalization 0.10.0

### New — `Faolline.GraphLocalization` (Runtime)

```csharp
/// Single source of every localization table name (build, import, runtime). Platform-stable.
public static class LocalizationTableNames
{
    public static string Sanitize(string name);
    public static string NormalizeGroup(string group);            // trim; empty/whitespace → null
    public static string GroupComparisonKey(string group);        // case-insensitive identity of a group
    public static string ForGraph(string graphName);              // per-graph table base name
    public static string ForGroup(string libName, string group);  // "{lib}_{group ?? "Global"}" (sanitized)
    public static string TextCollection(string table);            // "{table}_Text"
    public static string AssetCollection(string table, string assetType); // "{table}_{type}"
    public static IReadOnlyList<(int flag, string name)> AssetTypes { get; } // Audio, Sprite, Texture, Video, Font
}

/// Optional companion of ILocalizationProvider: resolve directly in a designated table.
public interface ITableScopedLocalizationProvider
{
    /// Same contract as ILocalizationProvider.Resolve: the value, or "#key" when the key is missing.
    string ResolveInTable(string table, string key, string locale);
}

/// Optional companion of ILocalizedAssetProvider.
public interface ITableScopedLocalizedAssetProvider
{
    T ResolveAssetInTable<T>(string table, string key) where T : UnityEngine.Object;
}

/// Dispatch helper: scoped call when supported and table non-empty, classic call otherwise.
public static class TableScopedLookup
{
    public static string Resolve(ILocalizationProvider provider, string table, string key, string locale);
    public static T ResolveAsset<T>(ILocalizedAssetProvider provider, string table, string key) where T : UnityEngine.Object;
}

/// Shared RFC4180 CSV helpers (multiline quoted fields supported).
public static class LocalizationCsv
{
    public static List<List<string>> ParseRecords(string csvText);
    public static string Escape(string field);
}
```

### Changed (additive)

```csharp
public class LocalizationKeyEntry { /* … */ public string Group; }   // NEW field (global keys)

public class LocalizationDatabase
{
    // was: AddGlobalKey(string key, LocalizationKeyType type, string defaultHint = "")
    public void AddGlobalKey(string key, LocalizationKeyType type, string defaultHint = "", string group = null);
    public IReadOnlyList<(string group, IReadOnlyList<LocalizationKeyEntry> keys)> GlobalKeysByGroup();  // NEW
}
```

### New — `Faolline.GraphLocalization.Editor`

```csharp
/// Pure routing of a global-key translation CSV to the collections holding each key.
public static class GlobalKeyCsvRouter
{
    public sealed class Result
    {
        public IReadOnlyDictionary<string, string> CsvByCollection { get; }  // header + routed rows
        public IReadOnlyList<string> Failures { get; }                        // "key 'x' found in no collection" / "…in several: a, b"
    }
    public static Result Route(string csvText, Func<string, IReadOnlyList<string>> collectionsHoldingKey);
}

public static class CsvLocalizationExporter
{
    // existing overload kept (delegates with carry = null)
    public static string BuildCsv(string existingCsv, IReadOnlyList<(string key, string hint)> desired,
        IReadOnlyList<string> locales, string sourceLocale,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> carry,   // NEW overload
        out List<(string locale, int filled, int total)> coverage, out int orphansRemoved);
}
```

### Changed behavior — `Faolline.GraphLocalization.Unity`

- `UnityLocalizationProvider` **implements** `ITableScopedLocalizationProvider`.
- `UnityLocalizedAssetProvider` **implements** `ITableScopedLocalizedAssetProvider`.
- Public constructors unchanged (still created by reflection from `LocalizationSettingsAsset`).

### Changed behavior — Editor (BREAKING, documented)

- `UnityLocalizationSyncer.SyncDatabase` (signature unchanged): global keys now go to `{lib}_{group}_Text` collections under `Collections/{lib}/_Global/`; `Global_Text` is no longer produced (its translations are carried over; it is reported as orphan).
- `TranslationImportBatch -speakersCsv`: rows routed by key instead of imported into `Global_Text`.

## com.faolline.graphdialoguesystem 0.20.0

```csharp
public class Speaker
{
    /// Optional localization table group. Empty = default speakers table.
    public string LocalizationGroup { get; set; }   // NEW
}

public static class DialogueLocalizationKeys
{
    public const string LibName = "GraphDialogue";                 // NEW (adapter LibName points here)
    public static string SpeakerTableGroup(Speaker speaker);      // NEW: "Speakers" | "Speakers_{group}"
    public static string ForSpeakerTable(Speaker speaker);        // NEW: table base name
    public static string ForSpeakerGroupTable(string group);      // NEW: table for a group value (inspector preview)
    public static string ForGraphTable(BaseGraph graph);          // NEW: ForGraph(graph.name); null graph → null
}

// Editor — backs the Speaker inspector's group dropdown (follow-up: no free-text field)
public static class SpeakerGroupCatalog
{
    public static IReadOnlyList<string> Collect();                                   // groups used by the project's speakers
    public static PopupModel BuildPopup(IReadOnlyList<string> groups, string current); // (None), groups, New group…
}

public sealed class DialoguePresenter
{
    // existing 2-arg overloads kept (unscoped, unchanged behavior)
    public DialogueStep Resolve(BaseNodeData node, BaseContext context, BaseGraph ownerGraph);           // NEW
    public LineStep ResolveLine(DialogueLineNodeData line, BaseContext context, BaseGraph ownerGraph);   // NEW
    public ChoiceStep ResolveChoice(ChoiceNodeData choiceNode, BaseContext context, BaseGraph ownerGraph); // NEW
}
```

Behavior: speaker names are always looked up in the speaker's table (scoped when the provider supports it); `DialoguePlayer` passes `BaseRunner.CurrentGraph` as owner.

## com.faolline.graphquest 0.12.1

No API change. `QuestEvaluator` journal texts are looked up in `ForGraph(quest.name)` when the provider supports scoping.

## com.faolline.graphimport 0.6.0

```csharp
// Runtime (noEngineReferences)
public sealed class SpeakerGroupMapping
{
    public static SpeakerGroupMapping Parse(string csvText);   // throws SpeakerGroupMappingException
    public bool TryGetGroup(string speakerKey, out string group);
    public int Count { get; }
}
public sealed class SpeakerGroupMappingException : Exception { }

// Editor
public sealed class ProjectAssetResolver : IProjectAssetResolver
{
    public ProjectAssetResolver(GenerationPlan plan, string speakerFolder, SpeakerGroupMapping mapping = null); // optional param added
}

public sealed class SpeakerGroupChange
{
    public string SpeakerKey { get; }
    public string AssetPath { get; }
    public string OldGroup { get; }
    public string NewGroup { get; }
}

public static class SpeakerGroupApplier
{
    public static IReadOnlyList<SpeakerGroupChange> Apply(IEnumerable<string> speakerKeys, SpeakerGroupMapping mapping);
}
```

`IProjectAssetResolver` is unchanged.
