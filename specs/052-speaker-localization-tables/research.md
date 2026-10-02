# Research: Separable Speaker Localization Tables

All Technical Context unknowns are resolved below. The user locked decisions A/B/C (see spec Input) during the design discussion. This file records the *implementation-level* decisions taken on top of them, each grounded in the current code.

---

## R1: One shared, platform-stable naming rule

**Decision**: Add a new `Faolline.GraphLocalization.LocalizationTableNames` (Runtime, static) as the single source of every table name:

- `Sanitize(name)` replaces the **fixed** set `" < > | : * ? \ /` plus control chars 0–31 with `_`. Empty or null gives `"Unnamed"`.
- `ForGraph(graphName)` = `Sanitize(graphName)`, the table *base* of a per-graph table.
- `ForGroup(libName, group)` = `Sanitize(libName) + "_" + Sanitize(NormalizeGroup(group) ?? "Global")`.
- `TextCollection(table)` = `table + "_Text"`; `AssetCollection(table, assetType)` = `table + "_" + assetType`.
- `AssetTypes` holds the `(flag, name)` map (`Audio`, `Sprite`, `Texture`, `Video`, `Font`), moved from `UnityLocalizationSyncer.AssetTypeMap`.
- `NormalizeGroup(group)` trims the value; empty or whitespace-only gives `null` ("no group").
- `GroupComparisonKey(group)` = `Sanitize(NormalizeGroup(group)).ToLowerInvariant()`.

**Rationale**:
- **Three copies exist today.** `UnityLocalizationSyncer.Sanitize`, `CsvLocalizationExporter.Sanitize` and `TranslationImportBatch.Sanitize` (the last with a "must stay identical" comment), plus an unused `LocalizationBuilderCore.SanitizeFileName`. FR-007 requires exactly one.
- **The copies can't simply move to the runtime.** They all use `Path.GetInvalidFileNameChars()`, which is platform-dependent: on Windows it covers the set above, on Linux/Android/macOS players only `/` and `\0`. A runtime lookup on device would compute a different name than the Windows editor that built the collection.
- **No rename for existing collections.** The fixed set equals the Windows set, so names built on the Windows dev machine (the dev project and Cryptique) are unchanged.

**Alternatives considered**:
- Keep `GetInvalidFileNameChars` at runtime. Rejected because names diverge per platform.
- Store resolved names in the manifest. Rejected because it needs a key→table index, which was already rejected in the spec.

---

## R2: Speaker group → global group

**Decision**: graphlocalization stays generic. The dialogue lib maps a speaker to a global group:

- A speaker with no `LocalizationGroup` goes to group `"Speakers"`.
- A speaker with group `G` goes to group `"Speakers_" + G`.

This yields the collections `GraphDialogue_Speakers_Text` and `GraphDialogue_Speakers_Chapitre1_Text`. `DialogueLocalizationKeys` gains `LibName = "GraphDialogue"` (the adapter's `LibName` points to it, so the runtime can compute the same name), plus `SpeakerTableGroup(speaker)` and `ForSpeakerTable(speaker)`.

**Rationale**:
- Matches the spec's example names.
- Keeps "Speakers" out of graphlocalization (Constitution II).
- Lib-scoped names also fix the latent cross-lib `Global_Text` collision: a future QuestGraph global key would land in `GraphQuest_Global_Text`.

**Alternatives**:
- `{lib}_{group}` with no `Speakers` prefix. Rejected because a speaker group named like another global group would merge silently.

---

## R3: Table-scoped lookups via optional companion interfaces

**Decision**: Add to graphlocalization Runtime:

```csharp
public interface ITableScopedLocalizationProvider
{   // same return contract as ILocalizationProvider.Resolve: value, or "#key" when missing
    string ResolveInTable(string table, string key, string locale);
}
public interface ITableScopedLocalizedAssetProvider
{
    T ResolveAssetInTable<T>(string table, string key) where T : UnityEngine.Object;
}
public static class TableScopedLookup
{
    public static string Resolve(ILocalizationProvider p, string table, string key, string locale);
    public static T ResolveAsset<T>(ILocalizedAssetProvider p, string table, string key) where T : Object;
}
```

`TableScopedLookup` uses the scoped method when the provider implements it **and** `table` is non-empty. Otherwise it calls the classic `Resolve` / `ResolveAsset`.

- **Implementers:** only `UnityLocalizationProvider` and `UnityLocalizedAssetProvider`.
- **Unchanged:** `CsvLocalizationProvider`, which loads every file up front so a table brings nothing, and `DialogueTitleProvider`, which has no tables.

**Rationale**:
- FR-010 and FR-011: the classic path is untouched.
- SC-006: no existing interface changes.
- Callers keep their existing missing-key handling (`#key` marker → strict/audit/title fallback) because the return contract is identical.

**Alternatives**:
- New member on `ILocalizationProvider`. Rejected because it breaks consumers.
- C# default interface member. Rejected because backend support is not guaranteed.
- Passing the table inside the key (`table/key`). Rejected because it breaks every provider and the CSV format.

---

## R4: Unity provider: targeted, never probing, with one guarded exception

**Decision**: `UnityLocalizationProvider.ResolveInTable(table, key, locale)` works in three cases:

1. **Known table** (`TextCollection(table)` is among the manifest collections the provider was built with): read only that collection. A key that is absent or untranslated everywhere returns `#key`. No probing (FR-009).
2. **Unknown table** (absent from the manifest list): log one warning per table name (`GraphLocalization.Playback` category), then fall back to the classic `Resolve(key, locale)`.
3. **Cache:** it never writes `_keyToCollection` for scoped calls, so scoped and unscoped lookups don't interfere.

`UnityLocalizedAssetProvider.ResolveAssetInTable<T>(table, key)` behaves the same way, over the candidates `AssetCollection(table, type)` for each entry in `LocalizationTableNames.AssetTypes` that is present in its manifest list. None present means unknown table: warn once and fall back to probing.

**Rationale**: The unknown-table fallback protects real cases where the runtime graph name differs from the built collection name:
- an `Object.Instantiate`d graph is named `X(Clone)`;
- a code-built graph or a test fixture was never synced.

Today these work by probing. Turning them into missing texts would be a regression. A *known* table missing a key is a stale build: missing text, fixed by rebuilding (spec assumption).

**Alternatives**:
- Strict "table or nothing". Rejected for the reason above.
- Strip a `(Clone)` suffix. Rejected as a one-off heuristic that is still incomplete.

---

## R5: Making "opens no other table" testable

**Decision**: Give the two Unity providers an internal reader seam:
- `internal interface IStringTableReader { bool TryRead(string collection, string key, out string value); }`
- an equivalent `IAssetTableReader`.

The default implementations wrap today's `UnityLocalizationSettings.StringDatabase` / `AssetDatabase` logic (selected locale first, then any-locale fallback), moved verbatim. An internal constructor accepts a reader. `AssemblyInfo.cs` declares `[InternalsVisibleTo("com.faolline.graphlocalization.Localization.Unity.Tests.EditMode")]`.

The new test assembly:
- has `defineConstraints: ["GRAPHLOCALIZATION_UNITY_LOCALIZATION"]`;
- references `com.faolline.graphlocalization.Runtime`, `.Localization.Unity`, `.Localization.Unity.Editor`, `.Editor`, `Unity.Localization`, `Unity.Localization.Editor` and the test runners;
- is registered in `DependencyMatrixTests` and `ARCHITECTURE.md`.

Tests assert the exact collections a fake reader was asked about.

**Rationale**: SC-001/SC-002 are about which tables get opened. Only a seam at the read boundary observes that deterministically, without Addressables/Unity Localization runtime initialization in EditMode.

---

## R6: Database: grouped global keys

**Decision**:
- `LocalizationKeyEntry` gains `public string Group`. It is meaningful for global keys and normalized via `NormalizeGroup`.
- `LocalizationDatabase.AddGlobalKey(string key, LocalizationKeyType type, string defaultHint = "", string group = null)`. The optional parameter is source-compatible.
  - **Duplicate key:** first add wins, unchanged. The *adapter* detects cross-group duplicates because only it knows asset paths (R7).
  - **Group spelling:** groups are canonicalized case-insensitively. The first spelling seen wins (comparison via `GroupComparisonKey`). A later variant that differs only by case, or sanitizes to the same name, reuses that spelling and is reported once as a `Logging.Warning`. This covers the spec edge case "not silently merged".
- New helper: `IReadOnlyList<(string group, IReadOnlyList<LocalizationKeyEntry> keys)> GlobalKeysByGroup()`, ordered by first appearance.

**Rationale**: Generic (FR-003). Deterministic because the adapter feeds speakers sorted by asset path.

---

## R7: Dialogue adapter: ordering and identifier conflicts

**Decision**: `DialogueGraphLocalizationAdapter.ExtractGlobalKeys`:
- loads every `Speaker` and sorts by asset path (ordinal);
- tracks `SpeakerId → (normalized group, path)`.

A second speaker with the same id **and a different group** triggers `Logging.Error("GraphLocalization.Validation", …)` naming both asset paths and both groups. The first (path order) keeps the key, so its translations are not dropped. Same id and same group: unchanged behavior (deduplicated silently, as today).

**Rationale**:
- FR-015: an explicit error naming both speakers.
- Keeping a deterministic placement instead of dropping the key avoids deleting its existing translations as orphans. SC-003 still holds in the error state.

---

## R8: Syncer: per-group collections and translation carry-over

**Decision**: `UnityLocalizationSyncer.SyncDatabase` (public signature unchanged) delegates to an internal overload with a `collectionsRoot` parameter. Changes:

**1. Per-group collections**
- Each `(group, keys)` from `GlobalKeysByGroup()` gets the collection `TextCollection(ForGroup(lib, group))`.
- Folder: `Collections/{lib}/_Global/{table}/`.
- Asset collections follow the same scheme, via `CreatePerTypeAssetCollections(table, …)`.

**2. Carry-over pre-pass**, run before any `SyncEntries`:
1. Build `desiredCollectionByKey` over every key (graph and global) for this lib.
2. Enumerate every String Table Collection whose path is under `Collections/{lib}/`. This includes orphans: the legacy `Global_Text`, a renamed graph's old collection, an emptied group.
3. For each entry whose key is desired in a **different** collection, record its per-locale non-empty values into `carry[key][localeCode]`. Collections are visited in ordinal name order, and the first non-empty value per (key, locale) wins.

**3. Gap filling.** `SyncEntries` fills a locale value **only when the target's value is empty**, in this order:
1. existing target value (kept);
2. carried value;
3. source-locale default hint (unchanged behavior).

The fill now covers new *and* existing entries with gaps.

**4. Orphan report.** `ReportOrphanCollections` is unchanged in substance. Its message now notes that the collection's translations, if any were still needed, were carried over and that the collection can be deleted manually. This satisfies FR-005: reported, not deleted.

**Rationale**:
- **Single pass, before removals.** Taking the snapshot before any `SyncEntries` means a key's old entry is captured before the old collection removes it as orphan, whatever the processing order.
- **Cost.** Bounded by entries in the lib's own collections, which the syncer already loads. Values are read only for moved keys, so a no-op build reads none.
- **Scope.** Carry-over applies to String Tables only. Asset Table entries (localized clips) are *not* carried: speakers produce no asset keys, and the asset-table loss on a graph rename predates this feature (noted as a follow-up, outside spec scope).

**Alternatives**:
- Rename/move the old collection instead of copying. Rejected because a key can move to a collection that already exists with other keys.
- Copy whole collections. Rejected because keys move individually.

---

## R9: CSV backend parity and the shared CSV parser

**Decision**:

**1. Shared parser.** New `LocalizationCsv` (graphlocalization Runtime, public static) offers `ParseRecords(string)` (RFC4180, multiline-quoted fields) and `Escape(string)`. `CsvLocalizationExporter` and `CsvLocalizationProvider` switch to it, deleting their two "kept in sync" private copies.

**2. Exporter.**
- One file per group: `{csvFolder}/{lib}/{ForGroup(lib, group)}.csv`, e.g. `Csv/GraphDialogue/GraphDialogue_Speakers_Chapitre1.csv`.
- Carry-over pre-pass: parse every existing `*.csv` in `{csvFolder}/{lib}/` (ordinal file order). For each desired file, a key absent from *that* file, or present with empty cells, takes the missing cells from the first other file holding the key.
- New `BuildCsv` overload with an optional `carry` map. The existing overload delegates with `null`, so the public API is preserved.
- Files under the lib folder that this build did not write are reported as unused (warning, not deleted). The legacy `GraphDialogue_Global.csv` is one of them. It is no longer referenced by the manifest, so the runtime ignores it.

**Rationale**: FR-006 parity. There is no runtime gain in CSV mode: `LocalizationSettingsAsset` appends every manifest CSV into one provider.

---

## R10: TranslationImportBatch: key-routed `-speakersCsv`

**Decision**: The pure logic `GlobalKeyCsvRouter.Route(string csvText, Func<string, IReadOnlyList<string>> collectionsHoldingKey)` lives in graphlocalization **Editor**, with no Unity Localization dependency, so it is tested in the existing test assembly. It returns:
- per-collection CSV texts (original header + that collection's rows, re-escaped via `LocalizationCsv.Escape`);
- failures: key held by no collection, or by more than one (ambiguous).

`TranslationImportBatch`:
- builds the `key → collections` index from every String Table Collection under the syncer's managed root `Assets/Localization/Collections/`, using a shared internal constant, no longer a literal;
- calls `Csv.ImportInto(reader, collection)` per bucket, where the default `removeMissingEntries = false` preserves other entries;
- reports each failure by key, still imports the valid rows, and exits 1 when any failure occurred (spec US3-2).

`-dialogueTranslationsDir` computes collection names via `TextCollection(ForGraph(fileName))`, replacing its private `Sanitize`.

**Rationale**:
- FR-012; "never guess".
- Keeping the `-speakersCsv` flag name preserves the dialogue-studio/Cryptique pipeline invocation. Its semantics generalize to "a CSV of global keys, routed by key".

**Alternatives**:
- Restricting candidates to `*_Speakers*` collections. Rejected because it puts dialogue knowledge into graphlocalization.

---

## R11: Runtime callers

**Decision**:

- **`DialoguePresenter`.** New overloads `Resolve(node, ctx, BaseGraph ownerGraph)`, `ResolveLine(line, ctx, BaseGraph ownerGraph)` and `ResolveChoice(choice, ctx, BaseGraph ownerGraph)`. The existing two-argument overloads delegate with `null`, which keeps the unscoped, classic behavior.
  - Line and choice text: `TableScopedLookup.Resolve(provider, ownerGraph != null ? ForGraphTable(ownerGraph) : null, key, locale)` inside `ResolveChecked`, so strict/audit/title-fallback handling is unchanged.
  - Voice: `TableScopedLookup.ResolveAsset<AudioClip>(assets, sameTable, key)`.
  - Speaker name: **always** scoped to `ForSpeakerTable(speaker)`. The presenter holds the `Speaker`, so no owner is needed.
- **`DialoguePlayer`.** Passes `_runner.CurrentGraph`, the top frame's graph, which is the sub-dialogue while one runs (spec US2-2).
- **Sample `GraphFlowDialogueSource`.** Passes `_driver.Runner.CurrentGraph`.
- **`QuestEvaluator.ResolveWithFallback`.** Uses `TableScopedLookup.Resolve(_localization, LocalizationTableNames.ForGraph(_quest.name), key, locale)`. Quest graphs are built per graph through `BaseGraphLocalizationAdapter`, so `{Sanitize(graph.name)}_Text` is their collection.
- **`LocalizationSettings.Resolve(key)` / `LocalizationContext.Resolve(key)`.** Unchanged and unscoped (FR-010).

**Rationale**: Every caller that knows its table now passes it, and nothing that doesn't changes behavior.

---

## R12: graphimport: mapping, creation, and re-import alignment

**Decision**:

**1. Mapping.** `SpeakerGroupMapping` (graphimport **Runtime**, pure, `noEngineReferences`). `Parse(string csvText)`:
- reads RFC4180 records through a small internal reader;
- requires header columns `SpeakerKey` and `Table` (ordinal, trimmed; other columns ignored);
- normalizes values (trim; empty Table means "no group");
- rejects with `SpeakerGroupMappingException`, before any asset write (FR-014):
  - a missing header column;
  - an empty `SpeakerKey` (1-based line number reported);
  - the same key listed with two different normalized groups (both values reported).
- An exact duplicate row is accepted.
- API: `bool TryGetGroup(string speakerKey, out string group)`, `int Count`.

**2. Creation.** `ProjectAssetResolver(plan, speakerFolder, SpeakerGroupMapping mapping = null)`: `CreateSpeaker` sets `LocalizationGroup` from the mapping (unlisted → empty).

**3. Alignment pass.** New Editor class `SpeakerGroupApplier.Apply(IEnumerable<string> speakerKeys, SpeakerGroupMapping mapping)`:
- for each distinct key that has an existing `Speaker` **and** a mapping row whose normalized group differs from the speaker's, it sets the group, marks the asset dirty and records a `SpeakerGroupChange(speakerKey, assetPath, oldGroup, newGroup)`;
- calls `AssetDatabase.SaveAssets()` once.

**4. Batch and window wiring.**
- `DialogueImportBatch` and `GraphImportWindow` parse the mapping before planning, then build plan → apply (creation uses the mapping) → `SpeakerGroupApplier.Apply(allReferencedSpeakerKeys, mapping)`.
- "All referenced speaker keys" means every `PivotLine.SpeakerKey` across **all** pivot dialogues of the set, including dialogues whose asset collided.
- Each change is printed: `[DialogueImportBatch] Speaker '<key>' group '<old>' → '<new>' (<path>)`.

**Rationale**:
- The never-overwrite rule makes every dialogue of a re-import collide, so speakers are never resolved during generation. Tying the update to generation would make US4-2 ("mapping wins on re-import") fail in the most common case. A separate pass over all referenced keys fixes that.
- Mapping rows for unreferenced speakers are ignored (spec edge case).
- No dialogue-studio or interchange change.
