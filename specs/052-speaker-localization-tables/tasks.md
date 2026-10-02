---
description: "Task list for feature implementation"
---

# Tasks: Separable Speaker Localization Tables

**Input**: Design documents from `/specs/052-speaker-localization-tables/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R12), data-model.md, contracts/public-api.md, contracts/cli.md, quickstart.md

**Tests**: Included and REQUIRED (constitution IV, NON-NEGOTIABLE). Every implementation task depends on the test task(s) written before it. A test that does not compile because the API does not exist yet counts as red. Run the tests headless:

```text
"<Unity 6000.0>" -batchmode -projectPath H:/repos/Perso/LIB_Unity/FaollineGraphEcosystem
    -runTests -testPlatform EditMode -testResults <file>.xml [-testFilter <regex>]
```

Never add `-quit`: it kills the run before the runner starts. Checkpoints always run the **full** suite, not a package-filtered one (lesson from 049).

**Organization**: Tasks are grouped by user story, US1–US4 from spec.md. Paths are relative to the repo root (`Assets/FaollineGraphEcosystem`). Five packages are touched: `com.faolline.graphlocalization`, `com.faolline.graphdialoguesystem`, `com.faolline.graphquest`, `com.faolline.graphimport`, plus `com.faolline.graphTest` (architecture guard).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Maps to US1–US4 from spec.md

---

## Phase 1: Setup

**Purpose**: Test infrastructure the later phases need. No production behavior changes.

- [X] T001 Create the gated test assembly `com.faolline.graphlocalization/Tests/Localization.Unity.EditMode/com.faolline.graphlocalization.Localization.Unity.Tests.EditMode.asmdef`. Settings:
  - name `com.faolline.graphlocalization.Localization.Unity.Tests.EditMode`, `includePlatforms: ["Editor"]`, `overrideReferences: true` with `nunit.framework.dll`, `autoReferenced: false`;
  - references `com.faolline.graphlocalization.Runtime`, `.Editor`, `.Localization.Unity`, `.Localization.Unity.Editor`, `com.faolline.graphlogging.Runtime`, `Unity.Localization`, `Unity.Localization.Editor`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`;
  - `defineConstraints: ["GRAPHLOCALIZATION_UNITY_LOCALIZATION"]` plus the same `versionDefines` entry as `com.faolline.graphlocalization/Localization.Unity/Runtime/com.faolline.graphlocalization.Localization.Unity.asmdef`.

  Register it in `com.faolline.graphTest/Tests/EditMode/Architecture/DependencyMatrixTests.cs` (new `LocUnityTests` const + allowed-references entry next to `LocTests`) and in the test-assembly table of `ARCHITECTURE.md`. Both go in the same commit.
- [X] T002 [P] Add `[assembly: InternalsVisibleTo("com.faolline.graphlocalization.Localization.Unity.Tests.EditMode")]` in new files `com.faolline.graphlocalization/Localization.Unity/Runtime/AssemblyInfo.cs` and `com.faolline.graphlocalization/Localization.Unity/Editor/AssemblyInfo.cs`, both wrapped in `#if GRAPHLOCALIZATION_UNITY_LOCALIZATION` like their siblings. Add `[assembly: InternalsVisibleTo("com.faolline.graphimport.Tests.EditMode")]` in new `com.faolline.graphimport/Editor/AssemblyInfo.cs`, mirroring `com.faolline.graphimport/Runtime/AssemblyInfo.cs`.
- [X] T003 Run the full EditMode suite on this branch before any change and record the baseline count (pass/fail/skip) in the T055 commit message draft. Pre-existing failures must be known before work starts.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared naming, scoped-lookup contracts, the shared CSV parser, and the `Speaker` group field. US1–US4 all build on these (research R1, R2, R3, R9).

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests (write first, confirm red)

- [X] T004 [P] Write `com.faolline.graphlocalization/Tests/EditMode/LocalizationTableNamesTests.cs`. Cover:
  - `Sanitize` replaces each of `" < > | : * ? \ /` and a control char with `_` (identical result on every platform, since there is no `Path.GetInvalidFileNameChars` dependency); `null`/`""` give `Unnamed`; legal names pass through unchanged;
  - `NormalizeGroup` trims, and whitespace gives `null`;
  - `GroupComparisonKey("Chap1") == GroupComparisonKey(" chap1 ")`;
  - `ForGraph("DLG_001") == "DLG_001"`;
  - `ForGroup("GraphDialogue", null) == "GraphDialogue_Global"` and `ForGroup("GraphDialogue", "Speakers_Chapitre1") == "GraphDialogue_Speakers_Chapitre1"`;
  - `TextCollection`/`AssetCollection` suffixes;
  - `AssetTypes` equals `(1<<1,"Audio"),(1<<2,"Sprite"),(1<<3,"Texture"),(1<<4,"Video"),(1<<5,"Font")` in that order.
- [X] T005 [P] Write `com.faolline.graphlocalization/Tests/EditMode/TableScopedLookupTests.cs`, using test-local fakes (a plain `ILocalizationProvider`, and one that also implements `ITableScopedLocalizationProvider` and records `(table, key)`). Cover:
  - a scoped provider with a non-empty table calls `ResolveInTable` and never `Resolve`;
  - a `null`/`""` table calls `Resolve`;
  - a plain provider always calls `Resolve`;
  - the `#key` return contract passes through unchanged;
  - the same four cases for `ResolveAsset<T>`, with fake `ILocalizedAssetProvider` / `ITableScopedLocalizedAssetProvider`.
- [X] T006 [P] Write `com.faolline.graphlocalization/Tests/EditMode/LocalizationCsvTests.cs` for `LocalizationCsv.ParseRecords`: plain row; comma inside quotes; doubled quotes; newline inside quotes; CRLF and lone CR line ends; blank lines skipped; trailing record without newline. For `Escape`: no-op on plain text; quoting on `,` `"` `\n` `\r`; `ParseRecords(Escape(x))` round-trips for each case.
- [X] T007 [P] Write `com.faolline.graphdialoguesystem/Tests/EditMode/Runtime/SpeakerLocalizationGroupTests.cs`. Cover:
  - a new `Speaker` has `LocalizationGroup == ""`;
  - `DialogueLocalizationKeys.SpeakerTableGroup` gives `"Speakers"` for empty/whitespace and `"Speakers_Chapitre1"` for `" Chapitre1 "`;
  - `ForSpeakerTable` gives `"GraphDialogue_Speakers"` / `"GraphDialogue_Speakers_Chapitre1"`;
  - `ForGraphTable(null) == null`; `ForGraphTable(graph)` equals `LocalizationTableNames.ForGraph(graph.name)`;
  - `DialogueLocalizationKeys.LibName == "GraphDialogue"`.

### Implementation

- [X] T008 Implement `com.faolline.graphlocalization/Runtime/LocalizationTableNames.cs` per data-model.md "Naming rules", with XML docs (depends on: T004).
- [X] T009 [P] Implement `ITableScopedLocalizationProvider.cs`, `ITableScopedLocalizedAssetProvider.cs` and `TableScopedLookup.cs` in `com.faolline.graphlocalization/Runtime/` per contracts/public-api.md, with XML docs that state the `#key` contract (depends on: T005).
- [X] T010 [P] Implement `com.faolline.graphlocalization/Runtime/LocalizationCsv.cs` (move the RFC4180 tokenizer + `Escape`). Switch `com.faolline.graphlocalization/Runtime/CsvLocalizationProvider.cs` and `com.faolline.graphlocalization/Editor/CsvLocalizationExporter.cs` to it and delete their private copies, including the "kept in sync" comments. Existing `CsvLocalizationProviderTests` / `CsvLocalizationExporterTests` stay green (depends on: T006).
- [X] T011 Replace every private sanitize/asset-map copy with `LocalizationTableNames` (no behavior change for Windows-built names):
  - `UnityLocalizationSyncer.Sanitize` and `AssetTypeMap` in `com.faolline.graphlocalization/Localization.Unity/Editor/UnityLocalizationSyncer.cs`;
  - `CsvLocalizationExporter.Sanitize`;
  - `TranslationImportBatch.Sanitize`, whose `-dialogueTranslationsDir` collection name becomes `TextCollection(ForGraph(name))`, in `com.faolline.graphlocalization/Localization.Unity/Editor/Batch/TranslationImportBatch.cs`;
  - delete the unused `LocalizationBuilderCore.SanitizeFileName` in `com.faolline.graphlocalization/Editor/LocalizationBuilderCore.cs`.

  Also expose the managed root as `internal const string CollectionsRoot` on the syncer, for reuse by T040 (depends on: T008).
- [X] T012 Add `[SerializeField] private string _localizationGroup = string.Empty;` and the `LocalizationGroup` property (XML doc + tooltip) to `com.faolline.graphdialoguesystem/Runtime/Speakers/Speaker.cs`. Add `LibName`, `SpeakerTableGroup`, `ForSpeakerTable` and `ForGraphTable` to `com.faolline.graphdialoguesystem/Runtime/Localization/DialogueLocalizationKeys.cs`, using `LocalizationTableNames` (depends on: T007, T008).
- [X] T013 Checkpoint: full suite green (baseline + T004–T007 now passing).

**Checkpoint**: Naming, scoped contracts, CSV parser and the `Speaker` group exist. User stories can start.

---

## Phase 3: User Story 1 - Assign speakers to separate localization tables (Priority: P1) 🎯 MVP

**Goal**: The build produces one lib-scoped speaker table per group, carries translations when a key changes table, migrates the legacy `Global_Text`, and the CSV backend mirrors the split.

**Independent Test**: Three speakers (`Chapter1`, `Chapter2`, no group), then a build. Expect three distinct speaker tables, each holding only its speaker's name with the source text pre-filled. Then change a group and rebuild: the translations follow.

### Tests for User Story 1 ⚠️ write first, confirm they fail

- [X] T014 [P] [US1] Extend `com.faolline.graphlocalization/Tests/EditMode/LocalizationDatabaseTests.cs`. Cover:
  - `AddGlobalKey(k, SpeakerName, hint, " G ")` stores `Group == "G"`; a whitespace group gives `null`;
  - `GlobalKeysByGroup()` returns groups in first-appearance order, each with its keys;
  - a case-only variant (`"g"` after `"G"`) reuses spelling `"G"` and logs one `LogType.Warning` (`LogAssert.Expect`);
  - a duplicate key keeps the first entry;
  - the 3-argument call still compiles and behaves as before.
- [X] T015 [P] [US1] Extend `com.faolline.graphlocalization/Tests/EditMode/CsvLocalizationExporterTests.cs` for the new `BuildCsv(..., carry, ...)` overload:
  - a key absent from `existingCsv` takes every locale from `carry`;
  - an existing non-empty cell is never overwritten by `carry`;
  - an empty existing cell is filled from `carry`;
  - the source-locale hint applies only when neither existing nor carry has a value;
  - the old overload's output is unchanged (byte-identical to a pre-change fixture).
- [X] T016 [P] [US1] Write `com.faolline.graphdialoguesystem/Tests/EditMode/Editor/DialogueLocalizationAdapterGroupTests.cs`. Setup: create `Speaker` assets in a GUID-named temp folder under `Assets/`, with unique `SpeakerId`s (a GUID suffix, because the dev project holds other speakers; filter assertions to the test ids); delete the folder in TearDown. Run `new DialogueGraphLocalizationAdapter().ScanAndIndex(db)`. Cover:
  - each test speaker's `speaker_*` key is in group `Speakers` / `Speakers_Chapitre1` according to its `LocalizationGroup`;
  - two speakers with the same id and the same group produce one key and no error;
  - the same id with different groups gives one `LogAssert.Expect(LogType.Error, …)` whose message contains both asset paths, and the key keeps the group of the path-order-first asset.
- [X] T017 [P] [US1] Write `com.faolline.graphlocalization/Tests/Localization.Unity.EditMode/UnityLocalizationSyncerGroupTests.cs`. Setup:
  - call the internal `SyncDatabase` overload with `collectionsRoot` = a GUID-named temp folder under `Assets/` and a unique lib name, so no real collection is touched;
  - `Assume.That(LocalizationEditorSettings.GetLocales().Count >= 2)`;
  - delete created collections and the folder in TearDown.

  Cover:
  - (a) global keys in groups `Speakers` and `Speakers_A` create `{lib}_Speakers_Text` and `{lib}_Speakers_A_Text` under `{root}/{lib}/_Global/{table}/`, each with only its keys and the source column pre-filled;
  - (b) a key with a non-source translation in group A, re-synced in group B: the value is in B, absent from A;
  - (c) an orphan collection under `{root}/{lib}/` (stands in for the legacy `Global_Text`; don't use that literal name, it exists in the dev project) holding a translated key that is now desired in a group table: the value is migrated, the orphan is reported (warning) and still exists;
  - (d) a target with a non-empty value is not overwritten by carry;
  - (e) a per-graph key whose graph entry is renamed: its translation follows into the new `{newName}_Text`.

### Implementation for User Story 1

- [X] T018 [US1] Implement `Group` on `LocalizationKeyEntry`, `AddGlobalKey(..., string group = null)` with normalization and case-insensitive canonicalization (warning on a variant), and `GlobalKeysByGroup()` in `com.faolline.graphlocalization/Runtime/LocalizationDatabase.cs` (depends on: T014, T008).
- [X] T019 [P] [US1] Add a "Localization" section to `com.faolline.graphdialoguesystem/Editor/Inspector/SpeakerEditor.cs`: a `Localization Group` property field (tooltip: empty = default speakers table) and a read-only label showing `DialogueLocalizationKeys.ForSpeakerTable(target)` (depends on: T012).
- [X] T020 [US1] Update `com.faolline.graphdialoguesystem/Editor/Localization/DialogueGraphLocalizationAdapter.cs`:
  - `LibName => DialogueLocalizationKeys.LibName`;
  - `ExtractGlobalKeys` sorts speaker asset paths (ordinal) and passes `group: DialogueLocalizationKeys.SpeakerTableGroup(speaker)`;
  - it tracks `SpeakerId → (normalized group, path)` and logs `Logging.Error("GraphLocalization.Validation", …)` naming both paths and both groups when the same id arrives with a different group.

  (depends on: T016, T018, T012)
- [X] T021 [US1] Update `com.faolline.graphlocalization/Localization.Unity/Editor/UnityLocalizationSyncer.cs` per research R8:
  - `SyncDatabase` delegates to `internal static string[] SyncDatabase(..., string collectionsRoot)`;
  - one collection `TextCollection(ForGroup(lib, group))` per `GlobalKeysByGroup()` entry, in `{lib}/_Global/{table}/`, with asset collections via `CreatePerTypeAssetCollections(table, …)`; `"Global_Text"` is no longer produced;
  - carry-over pre-pass over every String Table Collection under `{root}/{lib}/`, run before any `SyncEntries` (ordinal collection order, first non-empty value per key×locale, only for keys desired in a different collection);
  - `SyncEntries(…, carry)` fills empty values (target value, then carried, then source hint) for every locale, for new and existing entries;
  - the orphan-collection warning says the translations were carried over and the collection can be deleted manually.

  (depends on: T017, T018, T011)
- [X] T022 [US1] Update `com.faolline.graphlocalization/Editor/CsvLocalizationExporter.cs` per research R9:
  - one file per `GlobalKeysByGroup()` entry at `{folder}/{lib}/{ForGroup(lib, group)}.csv`;
  - carry-over pre-pass parsing every existing `*.csv` in `{folder}/{lib}/` (ordinal) into `key → locale → value`;
  - new `BuildCsv` overload with `carry` (the old overload delegates with `null`);
  - warning listing `*.csv` files in the lib folder that this build did not write (not deleted).

  (depends on: T015, T018, T010)
- [X] T023 [US1] Checkpoint: full suite green. Then validate US1 acceptance scenarios 1–5 against T014–T017 (post-delivery gate).

**Checkpoint**: US1 delivers separable speaker tables at build time, with lossless moves and the legacy migration.

---

## Phase 4: User Story 2 - Playing a dialogue only opens the tables it needs (Priority: P1)

**Goal**: Lines, choices, voice clips, speaker names and quest texts are looked up in their own table only. Custom/CSV/title providers and unscoped calls are unchanged.

**Independent Test**: Two dialogues with their own tables, speakers in two groups, a recording reader. Playing one dialogue (including a sub-dialogue) reads only that dialogue's table(s) and the shown speakers' group tables.

### Tests for User Story 2 ⚠️ write first, confirm they fail

- [X] T024 [P] [US2] Write `com.faolline.graphlocalization/Tests/Localization.Unity.EditMode/UnityLocalizationProviderScopedTests.cs`. Use the internal constructor with a recording fake `IStringTableReader` and manifest list `[D1_Text, D2_Text, GraphDialogue_Speakers_A_Text]`. Cover:
  - `ResolveInTable("D1", k, l)` reads only `D1_Text`;
  - a key missing from `D1_Text` returns `#k` with no other collection read;
  - an unknown table `"X(Clone)"` logs one warning (`LogAssert`), then probes in manifest order; a second call for the same table logs nothing more;
  - classic `Resolve(k, l)` behaves exactly as before (probe order, cache);
  - a scoped call neither reads nor writes the classic key→collection cache.
- [X] T025 [P] [US2] Write `com.faolline.graphlocalization/Tests/Localization.Unity.EditMode/UnityLocalizedAssetProviderScopedTests.cs`. Use a recording fake `IAssetTableReader` and manifest `[D1_Audio, D1_Sprite, D2_Audio]`. Cover:
  - `ResolveAssetInTable<AudioClip>("D1", k)` reads only `D1_*` candidates present in the manifest;
  - an unknown table logs one warning and probes;
  - classic `ResolveAsset` is unchanged.
- [X] T026 [P] [US2] Write `com.faolline.graphdialoguesystem/Tests/EditMode/Runtime/DialoguePresenterScopedLookupTests.cs`. Use test-local recording fakes implementing `ILocalizationProvider + ITableScopedLocalizationProvider` and `ILocalizedAssetProvider + ITableScopedLocalizedAssetProvider`. Cover:
  - `ResolveLine(line, ctx, owner)` resolves the line key and voice in `ForGraphTable(owner)`;
  - `ResolveChoice(choice, ctx, owner)` resolves labels in the same table;
  - the speaker name resolves in `ForSpeakerTable(speaker)`, also via the 2-argument overloads;
  - the 2-argument `ResolveLine`/`ResolveChoice`/`Resolve` call classic `Resolve` for line/choice keys;
  - a scoped miss (`#key`) gives the same Permissive/Audit/Strict/titleFallback outcomes as the existing `DialoguePresenterTests`.
- [X] T027 [P] [US2] Write `com.faolline.graphdialoguesystem/Tests/EditMode/Runtime/DialoguePlayerScopedLookupTests.cs`. Build parent + child graphs with `DialogueGraphBuilder` (`AddSubGraph`), named `"Parent"` / `"Child"`. With the recording scoped provider, play to the end: every parent line was resolved in table `"Parent"`, every child line in `"Child"`, and no other table was requested.
- [X] T028 [P] [US2] Extend `com.faolline.graphquest/Tests/EditMode/QuestLocalizationTests.cs`. With a recording scoped provider and a quest graph named `"Q_Test"`: `DisplayName`, `Description` and every `GetObjectives()` label/description are resolved with table `"Q_Test"`; with the existing CSV provider, results are unchanged.

### Implementation for User Story 2

- [X] T029 [US2] Add the internal seams in `com.faolline.graphlocalization/Localization.Unity/Runtime/`:
  - `IStringTableReader.cs` + `UnityStringTableReader.cs` (move today's `TryResolveIn` body verbatim: selected locale first, then any-locale fallback);
  - `IAssetTableReader.cs` + `UnityAssetTableReader.cs` (move `TryResolve<T>`);
  - internal constructors on both providers taking a reader; public constructors use the Unity readers.

  All files are wrapped in `#if GRAPHLOCALIZATION_UNITY_LOCALIZATION` (depends on: T024, T025).
- [X] T030 [US2] Make `com.faolline.graphlocalization/Localization.Unity/Runtime/UnityLocalizationProvider.cs` implement `ITableScopedLocalizationProvider` per research R4: known table reads only `TextCollection(table)` (no probe, no cache write); unknown table gives a warn-once (`HashSet<string>`) then classic `Resolve` (depends on: T029, T009).
- [X] T031 [US2] Make `com.faolline.graphlocalization/Localization.Unity/Runtime/UnityLocalizedAssetProvider.cs` implement `ITableScopedLocalizedAssetProvider`: candidates are `AssetCollection(table, type)` for each `LocalizationTableNames.AssetTypes` entry present in the manifest list; none present gives a warn-once then classic `ResolveAsset` (depends on: T029, T009).
- [X] T032 [US2] Update `com.faolline.graphdialoguesystem/Runtime/Playback/DialoguePresenter.cs`:
  - add the `Resolve`/`ResolveLine`/`ResolveChoice` overloads with `BaseGraph ownerGraph`; the existing overloads delegate with `null`;
  - `ResolveChecked(key, fallbackTitle, table)` uses `TableScopedLookup.Resolve`;
  - the voice uses `TableScopedLookup.ResolveAsset<AudioClip>`;
  - `ResolveSpeakerName` uses `TableScopedLookup.Resolve(…, ForSpeakerTable(speaker), …)`.

  Update the XML docs, keeping "runner-agnostic" wording plus the owner note (depends on: T026, T009, T012).
- [X] T033 [US2] Update `com.faolline.graphdialoguesystem/Runtime/Playback/DialoguePlayer.cs` so `BuildLineStep`/`BuildChoiceStep` pass `_runner.CurrentGraph`; this covers the `HandleWaitingForSignal`/`HandleWaitingForTime` paths, which go through `BuildLineStep` (depends on: T027, T032).
- [X] T034 [P] [US2] Update `QuestEvaluator.ResolveWithFallback` in `com.faolline.graphquest/Runtime/QuestEvaluator.cs` to use `TableScopedLookup.Resolve(_localization, _quest != null ? LocalizationTableNames.ForGraph(_quest.name) : null, key, _localization.CurrentLocale)`, keeping the exact `#key` comparison (depends on: T028, T009, T008).
- [X] T035 [P] [US2] Update the sample `com.faolline.graphdialoguesystem/Samples~/GameFlowDialogueBridge/GraphFlowDialogueSource.cs` to call `_presenter.Resolve(node, _driver.Context, _driver.Runner.CurrentGraph)`. `Samples~` is not compiled in the dev project: verify by reading against `GraphFlowDriver.Runner` (`com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs:106`).
- [X] T036 [US2] Checkpoint: full suite green, with every pre-existing `DialoguePresenterTests`, `DialoguePlayer*Tests`, `QuestLocalizationTests` and UI test unchanged and passing. Validate US2 acceptance scenarios 1–6.

**Checkpoint**: US1 and US2 together give the real Addressables gain: separable tables, and only the needed ones are opened.

---

## Phase 5: User Story 3 - One speaker translation file reaches the right tables (Priority: P2)

**Goal**: `TranslationImportBatch -speakersCsv` routes each row to the collection holding its key, reports unknown and ambiguous keys, and still imports the valid rows.

**Independent Test**: One CSV with rows for three speakers in three groups, plus one unknown key. Each known row lands in its table; the unknown one is reported by key; the run reports failure.

### Tests for User Story 3 ⚠️ write first, confirm they fail

- [X] T037 [P] [US3] Write `com.faolline.graphlocalization/Tests/EditMode/GlobalKeyCsvRouterTests.cs`. Cover:
  - rows are routed by a fake `collectionsHoldingKey`, and each bucket's CSV keeps the original header;
  - a quoted multiline value survives routing (parse the bucket back with `LocalizationCsv`);
  - a key held by no collection gives a failure naming the key;
  - a key held by two collections gives an "ambiguous" failure naming both;
  - valid rows are still routed when other rows fail;
  - header-only or empty input gives no buckets and no failures.
- [X] T038 [P] [US3] Write `com.faolline.graphlocalization/Tests/Localization.Unity.EditMode/TranslationImportBatchRoutingTests.cs`. Create two uniquely named group collections under a temp root via the internal syncer overload (T021), then call the internal `TranslationImportBatch.ImportGlobalKeysCsv(csvPath, collectionsRoot)` (returns `(imported, failures)`, never calls `EditorApplication.Exit`). Cover:
  - each row's `fr` value lands in its own collection and other entries of each collection are untouched;
  - an unknown key is reported and the other rows are imported.

  `Assume` at least 2 locales, as in T017.

### Implementation for User Story 3

- [X] T039 [US3] Implement `com.faolline.graphlocalization/Editor/GlobalKeyCsvRouter.cs` per contracts/public-api.md (pure, uses `LocalizationCsv`) (depends on: T037, T010).
- [X] T040 [US3] Update `com.faolline.graphlocalization/Localization.Unity/Editor/Batch/TranslationImportBatch.cs`:
  - extract `internal static (int imported, List<string> failures) ImportGlobalKeysCsv(string csvPath, string collectionsRoot)`, which builds a `key → collections` index from every String Table Collection whose asset path is under `collectionsRoot`, runs `GlobalKeyCsvRouter.Route`, then `Csv.ImportInto(new StringReader(bucket), collection)` per bucket;
  - `-speakersCsv` calls it with `UnityLocalizationSyncer.CollectionsRoot`;
  - update the class XML doc (routing semantics, exit 1 on any failure) and drop every `"Global_Text"` literal.

  (depends on: T038, T039, T011, T021)
- [X] T041 [US3] Checkpoint: full suite green. Validate US3 acceptance scenarios 1–3.

---

## Phase 6: User Story 4 - Dialogue import assigns speaker groups from a mapping (Priority: P2)

**Goal**: `-speakerTablesCsv` sets the group of created speakers and realigns existing referenced speakers, even when every dialogue asset collides on re-import. An invalid mapping aborts before any write.

**Independent Test**: An export referencing 3 speakers, with a mapping listing 2. First run: created speakers carry the mapped groups. Second run with a changed mapping (all dialogues now collide): the existing speaker's group is updated and the change is printed.

### Tests for User Story 4 ⚠️ write first, confirm they fail

- [X] T042 [P] [US4] Write `com.faolline.graphimport/Tests/EditMode/SpeakerGroupMappingTests.cs` (pure). Cover:
  - header in any order, extra columns ignored;
  - quoted key containing a comma;
  - an empty `Table` gives `TryGetGroup` true with an empty/null group;
  - values are trimmed;
  - an exact duplicate row is accepted;
  - the same key with different groups throws `SpeakerGroupMappingException`, with a message containing the key and both values;
  - a missing `SpeakerKey` or `Table` column throws, naming the column;
  - an empty key throws, naming the 1-based line;
  - an unknown key gives `TryGetGroup` false;
  - `Count` counts distinct keys.
- [X] T043 [P] [US4] Extend `com.faolline.graphimport/Tests/EditMode/ProjectAssetResolverTests.cs`. Cover:
  - with a mapping, a created speaker has the mapped `LocalizationGroup`;
  - an unlisted created speaker has an empty group;
  - without a mapping, behavior is identical to today;
  - an existing speaker is returned unmodified by `ResolveSpeaker`, since the resolver never updates it.
- [X] T044 [P] [US4] Write `com.faolline.graphimport/Tests/EditMode/SpeakerGroupApplierTests.cs` (temp-folder `Speaker` assets with GUID-suffixed ids). Cover:
  - an existing listed speaker with a different group is updated, giving one `SpeakerGroupChange(key, path, old, new)`;
  - an equal group gives no change;
  - an unlisted speaker is untouched;
  - a key with no asset is ignored;
  - duplicate keys in the input are processed once;
  - the asset is saved (reload via `AssetDatabase` shows the new group).
- [X] T045 [P] [US4] Write `com.faolline.graphimport/Tests/EditMode/DialogueImportBatchSpeakerGroupTests.cs` against the internal `DialogueImportBatch.Execute(IReadOnlyDictionary<string,string> args, TextWriter output, TextWriter error)`, which returns the exit code. Use temp folders, a small interchange JSON fixture with 2 dialogues / 3 speaker keys, and a temp mapping CSV. Cover:
  - (a) first run: created speakers carry the mapped groups and the unlisted one is empty;
  - (b) change one mapping row and re-run (every dialogue asset collides): the existing speaker's group is updated and `output` contains `Speaker '<key>' group '<old>' → '<new>'`; the exit code stays driven by the conflicts only, exactly as before;
  - (c) an invalid mapping returns exit 1, writes the error, and no dialogue or speaker asset is created;
  - (d) without `-speakerTablesCsv`, the outcome is identical to the pre-feature behavior;
  - (e) a mapping file path that does not exist returns exit 1 before any write.

### Implementation for User Story 4

- [X] T046 [US4] Implement `com.faolline.graphimport/Runtime/SpeakerGroups/SpeakerGroupMapping.cs` (with a small internal RFC4180 reader: `graphimport.Runtime` is `noEngineReferences` and cannot use `LocalizationCsv`, see plan Complexity Tracking) and `com.faolline.graphimport/Runtime/SpeakerGroups/SpeakerGroupMappingException.cs` (depends on: T042).
- [X] T047 [US4] Add the optional `SpeakerGroupMapping mapping = null` constructor parameter to `com.faolline.graphimport/Editor/Resolution/ProjectAssetResolver.cs`; `CreateSpeaker` sets `LocalizationGroup` from it (depends on: T043, T046, T012).
- [X] T048 [US4] Implement `com.faolline.graphimport/Editor/Resolution/SpeakerGroupApplier.cs` and `SpeakerGroupChange.cs` per contracts/public-api.md. Compare `(speaker.LocalizationGroup ?? "").Trim()` with the mapping's already-normalized value (`?? ""`). These are the same semantics as `NormalizeGroup` (trim; empty = none), so there is **no** new asmdef reference from graphimport to graphlocalization (depends on: T044, T046, T012).
- [X] T049 [US4] Update `com.faolline.graphimport/Editor/Batch/DialogueImportBatch.cs`:
  - extract `internal static int Execute(args, output, error)` (`Run` calls it, then `EditorApplication.Exit(code)`);
  - read and parse `-speakerTablesCsv` **before** planning (a missing file or a parse error writes to `error` and returns 1, with nothing written);
  - pass the mapping to `ProjectAssetResolver`;
  - after `PlanApplier.Apply`, collect every `PivotLine.SpeakerKey` across **all** pivot dialogues, run `SpeakerGroupApplier.Apply`, and print each change;
  - update the class XML doc command line.

  (depends on: T045, T047, T048)
- [X] T050 [US4] Update `com.faolline.graphimport/Editor/Window/GraphImportWindow.cs`:
  - add a "Speaker tables CSV (optional)" text field next to "Speaker folder";
  - on Apply, parse it first (an error is logged via `Logging.Error("GraphImport", …)` and aborts before any write);
  - pass it to `ProjectAssetResolver`;
  - after apply, run `SpeakerGroupApplier` over all referenced keys and log each change.

  (depends on: T046, T047, T048)
- [X] T051 [US4] Checkpoint: full suite green. Validate US4 acceptance scenarios 1–6 and the spec edge cases "empty Table" and "unreferenced mapping row".

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T052 [P] Bump versions and floors in `package.json` and add the CHANGELOG entries:
  - `com.faolline.graphlocalization` → **0.10.0**. CHANGELOG `### BREAKING`: `Global_Text` is replaced by `{Lib}_{Group}_Text` collections; translations are carried over automatically on the first build; the old collection is reported and is safe to delete; `-speakersCsv` now routes by key; scripts referencing `Global_Text` must be updated. Then `### Added`: scoped providers, `LocalizationTableNames`, `LocalizationCsv`, carry-over.
  - `com.faolline.graphdialoguesystem` → **0.20.0** (floor graphlocalization 0.10.0).
  - `com.faolline.graphquest` → **0.12.1** (floor graphlocalization 0.10.0).
  - `com.faolline.graphimport` → **0.6.0** (floor graphdialoguesystem 0.20.0).
- [X] T053 [P] Update the READMEs:
  - `com.faolline.graphlocalization/README.md`: grouped global tables, scoped lookups and the Addressables note (libs produce separable collections; group assignment is the project's Addressables group rules), `-speakersCsv` routing; fix the `speaker_npc_mayor`/`Global_Text` passages.
  - `com.faolline.graphdialoguesystem/README.md`: the Speaker "Localization Group", the presenter `ownerGraph` overload in the render-bridge section.
  - `com.faolline.graphimport/README.md`: `-speakerTablesCsv`, the mapping format, re-import alignment.
  - `com.faolline.graphquest/README.md`: a one-line note on scoped lookup.
- [X] T054 [P] Sync the root docs (`README.md`, `INSTALL.md` version/floor tables and any dependency floor quoted elsewhere) with T052 (recurring floor-drift lesson).
- [X] T055 Final full EditMode suite (batchmode, no `-quit`): all green, `DependencyMatrixTests` included. Compare against the T003 baseline and record both counts in the final commit.
- [X] T056 Run quickstart.md steps 1–6 in the dev project, including the real migration of its existing `Assets/Localization/Collections/GraphDialogue/_Global/Global_Text`. Report the outcome to the user (dev-project assets live outside this repo and are not committed).
  - **Status (2026-10-03), run in the live Editor through the Unity Pipeline CLI:**
    - **Steps 1, 4, 5:** dev project switched temporarily to UnityLocalization mode, **Build All Tables** → `GraphDialogue_Speakers_Text` created with `speaker_npc_mayor` = `Mayor` / `Le Maire` carried over from `Global_Text` (syncer: "Carried 2 existing translation(s)"); `Global_Text` reported as orphan, kept; one table per demo group with only its speaker; manifest lists the new tables, not `Global_Text`.
    - **Steps 2, 3:** Play mode, `SampleDialogue` (with its sub-dialogue) played through `DialoguePlayer` + the real `UnityLocalizationProvider`. Unity Localization's loaded-table cache shows only `SampleDialogue_Text`, `SampleSubDialogue_Text`, `GraphDialogue_Speakers_Text` (speaker shown as "Le Maire"); a quest journal loaded only `NewQuest_Text`; for contrast, a classic untargeted lookup also opened an unrelated group table.
    - **Step 6:** full EditMode suite through the Editor, 1471/1471 green.
    - Dev project restored afterwards (CSV mode, test translation and demo speakers/tables removed). No Addressables player build was made; the Speaker inspector was checked by tests only (the Editor window could not be captured while in the background).

- [X] T057 [US1] Follow-up (user feedback 2026-10-03: typing the group by hand is error-prone). Tests first in `com.faolline.graphdialoguesystem/Tests/EditMode/Editor/SpeakerGroupCatalogTests.cs` and `.../Runtime/SpeakerLocalizationGroupTests.cs`. Then:
  - new `com.faolline.graphdialoguesystem/Editor/Inspector/SpeakerGroupCatalog.cs`: the groups used by the project's speakers, and the popup model (None / groups / New group…);
  - `DialogueLocalizationKeys.ForSpeakerGroupTable(string)`;
  - `SpeakerEditor`: dropdown instead of a free-text field, inline *New group…*, `[CanEditMultipleObjects]`.
- [X] T058 Docs for T057: graphdialoguesystem CHANGELOG 0.20.0 and README, quickstart step 1, contracts/public-api.md, spec FR-001 note.
---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: depends on Setup. **Blocks all stories.**
- **US1 (Phase 3)** and **US2 (Phase 4)**: both depend only on Foundational. They are independent of each other: US2's speaker lookup only needs `ForSpeakerTable` (T012), not the US1 build changes; an unbuilt group table falls into the unknown-table fallback, so no regression.
- **US3 (Phase 5)**: depends on Foundational plus T021 (US1). Routing targets the per-group collections the syncer creates, and T038 uses the syncer's internal overload to create them.
- **US4 (Phase 6)**: depends on Foundational only (T012 for `Speaker.LocalizationGroup`). It is independent of US1–US3 at the code level; its value at runtime comes with US1+US2.
- **Polish (Phase 7)**: after all stories.

### Within Each User Story

- Test tasks first, confirmed red, then implementation in the listed order, then the checkpoint (full suite + acceptance scenarios).
- Same-file tasks are never marked [P]: T021/T040 both touch the Localization.Unity Editor assembly but different files; T032/T033 are sequential.

### Parallel Opportunities

- Phase 2: T004–T007 in parallel, then T009/T010 in parallel (T008 → T011/T012).
- US1: T014–T017 in parallel; T019 in parallel with T020–T022.
- US2: T024–T028 in parallel; T034/T035 in parallel with T029–T033.
- US1 and US2 phases can run in parallel after Phase 2.
- US4 can run in parallel with US1/US2/US3 after Phase 2.

---

## Parallel Example: User Story 2

```text
# Tests together (different files, all red first):
Task: "T024 UnityLocalizationProviderScopedTests.cs"
Task: "T025 UnityLocalizedAssetProviderScopedTests.cs"
Task: "T026 DialoguePresenterScopedLookupTests.cs"
Task: "T027 DialoguePlayerScopedLookupTests.cs"
Task: "T028 QuestLocalizationTests.cs (extend)"

# Then, independent implementations:
Task: "T034 QuestEvaluator scoped lookup"          # alongside T029→T030/T031 and T032→T033
Task: "T035 GraphFlowDialogueSource sample"
```

---

## Implementation Strategy

### MVP

US1 alone gives separable tables but **no runtime gain**: the provider still probes. The minimum shippable increment for the user's goal is **Phase 1 + 2 + US1 + US2**. Stop there and validate with quickstart steps 1–3 before US3/US4.

### Incremental Delivery

1. Setup + Foundational: naming, contracts, `Speaker` group (no behavior change).
2. US1: speaker tables split at build, translations preserved.
3. US2: targeted runtime lookup (dialogues + quests). **Addressables goal met.**
4. US3: translation pipeline (dialogue-studio `speakers.csv`) works with split tables.
5. US4: the import pipeline assigns groups from the project's spreadsheet.
6. Polish: versions, docs, final suite, quickstart on the dev project.

Commit per logical unit: one commit per checkpoint at minimum. Per the project rule, **no `Co-Authored-By` trailer**.

---

## Notes

- New files under packages need their Unity `.meta`. Let a batchmode run import them, then commit the generated `.meta` files with the source.
- Tests that create assets use GUID-named temp folders under `Assets/` and unique ids/collection names. The dev project holds real speakers and real collections (including a legacy `Global_Text`) that must never be touched by tests.
- `LogAssert.Expect` every warning/error a test deliberately triggers (unknown-table warn-once, group-variant warning, id-conflict error, orphan report), or the Test Runner fails the test on an unexpected log.
