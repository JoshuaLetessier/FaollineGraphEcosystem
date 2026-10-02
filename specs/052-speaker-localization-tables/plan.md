# Implementation Plan: Separable Speaker Localization Tables

**Branch**: `052-speaker-localization-tables` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/052-speaker-localization-tables/spec.md`

## Summary

Two halves, both required for a real Addressables gain:

1. **Build side.** Speaker display names stop going into one project-wide `Global_Text` collection. A `Speaker` gets an optional `LocalizationGroup`. `com.faolline.graphlocalization` learns a *generic* notion of grouped global keys: one collection per (lib, group), with lib-scoped names (`GraphDialogue_Speakers_Text`, `GraphDialogue_Speakers_Chapitre1_Text`). The Unity syncer and the CSV exporter carry existing translations whenever a key moves between tables of the same lib. That covers group changes, graph renames and the one-time migration out of the legacy `Global_Text`. `TranslationImportBatch -speakersCsv` routes each row to the collection that holds its key.
2. **Runtime side.** Callers that know the table pass it, and a table-aware provider resolves there directly instead of probing every manifest collection. The dialogue presenter knows the speaker (so its group) and the owning graph (`BaseRunner.CurrentGraph`, sub-dialogue aware). The quest evaluator knows its quest graph. The new optional companion interfaces `ITableScopedLocalizationProvider` and `ITableScopedLocalizedAssetProvider` leave every existing `ILocalizationProvider` working unchanged. One runtime naming class, `LocalizationTableNames`, is shared by the syncer, the exporter, the translation import and the providers. It replaces three `Sanitize` copies, one of which is platform-dependent at runtime.

`com.faolline.graphimport` gains an optional `-speakerTablesCsv` mapping (`SpeakerKey,Table`). Created speakers take the mapped group. A post-apply pass then aligns every *existing* referenced speaker with the mapping, so a re-import whose dialogue assets all collide still updates groups.

## Technical Context

**Language/Version**: C# (Unity 6000.0), same ecosystem conventions as 047–051

**Primary Dependencies**:
- `com.faolline.graphlocalization` 0.9.1 → **0.10.0**: grouped global keys, `LocalizationTableNames`, table-scoped provider interfaces and lookup helper, shared `LocalizationCsv` parser, syncer/exporter carry-over, key-routed translation import
- `com.faolline.graphdialoguesystem` 0.19.1 → **0.20.0**: `Speaker.LocalizationGroup`, inspector field, `DialogueLocalizationKeys` table helpers, adapter grouping and conflict report, owner-graph-aware `DialoguePresenter` overloads, `DialoguePlayer` passing `CurrentGraph`
- `com.faolline.graphquest` 0.12.0 → **0.12.1**: `QuestEvaluator` looks up in its quest's own table (no public API change)
- `com.faolline.graphimport` 0.5.1 → **0.6.0**: `SpeakerGroupMapping` (pure, Runtime), resolver and applier support, `-speakerTablesCsv`, window field
- `com.unity.localization` 1.5.12 (already present, adapter assemblies only): `Csv.ImportInto(reader, collection)` with the default `removeMissingEntries = false` (verified in `Library/PackageCache/com.unity.localization@b0a588a05f2a/Editor/Plugins/CSV/CSV.cs:127`), so importing a per-collection subset leaves the other entries untouched
- `com.faolline.graphcore`: **no change**. `BaseRunner.CurrentGraph` already exists (`BaseRunner.cs:45`)

**Storage**: Unity Localization String/Asset Table Collections under `Assets/Localization/Collections/{lib}/…`, CSV files under the settings' CSV folder, `Speaker` assets (one new serialized string field). The `GraphLocalizationManifest` format is unchanged; it still lists collection names and CSV files.

**Testing**: Unity Test Framework, EditMode only, run headless via Unity batchmode `-runTests -testPlatform EditMode` **without** `-quit`, on the **full** ecosystem suite (both are lessons from 049/050). Logic is pushed into pure, unit-testable pieces wherever possible: naming, lookup dispatch, database grouping, CSV build/carry, CSV routing, mapping parse. The Unity-backed provider gets an internal table-reader seam. The syncer gets an internal `collectionsRoot` overload so its tests run in a throwaway folder with unique collection names, never touching the dev project's real `Assets/Localization/Collections`.

**Target Platform**: Unity Editor (build, import, sync) + all player platforms (runtime lookup; naming must be platform-stable, see research R1)

**Project Type**: Library changes across 4 existing packages (no new package; one new test assembly)

**Performance Goals**:
- **Runtime:** a targeted lookup opens exactly one String Table collection (plus, for a voice clip, at most that table's asset collections). This replaces opening up to *every* manifest collection on a cache miss.
- **Build:** the carry-over pre-pass is linear in the number of entries across the lib's own collections. It runs inside the existing auto-build and must not make a no-op rebuild noticeably slower (it only reads values for keys that actually moved).

**Constraints**:
- Existing `ILocalizationProvider` / `ILocalizedAssetProvider` implementations in consumer projects compile and behave unchanged (SC-006): **no member added to existing interfaces**.
- Zero translations lost on group change, graph rename, or legacy migration (SC-003).
- Never guess: an unknown key in the speakers CSV and an invalid mapping are both explicit failures.
- A targeted miss is a missing text and never triggers a probe (FR-009). See R4 for the *unknown table* case.

**Scale/Scope**: Cryptique-scale. Hundreds of dialogue graphs, roughly a hundred speakers, a handful of chapter groups, 2–4 locales.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Foundation Stability | ✅ graphcore untouched. Uses the already-public `BaseRunner.CurrentGraph`. |
| II. Universal Abstractions Only | ✅ No speaker concept enters graphcore *or* graphlocalization: "group" is a generic attribute of global keys, and only graphdialoguesystem maps a speaker to the `Speakers[_<group>]` group. |
| III. Specification-First | ✅ `spec.md` written and clarified (FR-016 resolved: quests in scope) before this plan. |
| IV. TDD | ✅ Every task pairs a failing test first. Tests run headless via Unity batchmode, the ecosystem's established runner (Coplay MCP when available). EditMode only. |
| V. Simplicity | ⚠️ Four justified deviations. See Complexity Tracking. |
| VI. Typed Context Contract | N/A (no context keys added). |
| VII. Cross-lib via SubGraph only | ✅ No new cross-lib asmdef edge. graphimport → graphdialoguesystem is the existing T4 exception. The one new assembly (Unity-adapter tests) is registered in `DependencyMatrixTests` + `ARCHITECTURE.md` in the same commit. |
| Dev standards: dependencies | ✅ No new external dependency. `com.unity.localization` stays confined to the `Localization.Unity` adapter assemblies. The CSV provider still works without it. |
| Dev standards: logging/docs | ✅ All diagnostics through `Faolline.GraphLogging.Logging` (no raw `Debug.LogError`). XML `<summary>` on every new public type and member. |
| Semver gate | ✅ Per-package assessment in Summary/Technical Context. The `Global_Text` rename is called out as BREAKING in the graphlocalization 0.10.0 CHANGELOG, with its migration path. |

**Post-design re-check (after Phase 1)**: ✅ unchanged. The contracts add only optional interfaces, optional parameters, additive overloads and one serialized field. No existing signature is removed or altered.

## Project Structure

### Documentation (this feature)

```text
specs/052-speaker-localization-tables/
├── plan.md              # This file
├── research.md          # Phase 0: decisions R1–R12
├── data-model.md        # Phase 1: entities, naming rules, carry-over precedence
├── quickstart.md        # Phase 1: end-to-end validation in the dev project
├── contracts/
│   ├── public-api.md    # C# surface added/changed, per package
│   └── cli.md           # DialogueImportBatch / TranslationImportBatch + mapping file format
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
com.faolline.graphlocalization/
├── Runtime/
│   ├── LocalizationTableNames.cs                 # NEW: shared, platform-stable naming (R1)
│   ├── ITableScopedLocalizationProvider.cs       # NEW: optional companion interface (R3)
│   ├── ITableScopedLocalizedAssetProvider.cs     # NEW
│   ├── TableScopedLookup.cs                      # NEW: dispatch helper (falls back to Resolve)
│   ├── LocalizationCsv.cs                        # NEW: shared RFC4180 parse/escape (R9)
│   ├── CsvLocalizationProvider.cs                # uses LocalizationCsv (drops its private copy)
│   └── LocalizationDatabase.cs                   # LocalizationKeyEntry.Group, AddGlobalKey(..., group), group canonicalization
├── Editor/
│   ├── CsvLocalizationExporter.cs                # per-group files, carry-over, unused-file report
│   ├── GlobalKeyCsvRouter.cs                     # NEW: pure key→collection routing for -speakersCsv (R9)
│   └── LocalizationBuilderCore.cs                # drop private SanitizeFileName (unused)
├── Localization.Unity/
│   ├── Runtime/
│   │   ├── UnityLocalizationProvider.cs          # implements ITableScopedLocalizationProvider + reader seam
│   │   ├── UnityLocalizedAssetProvider.cs        # implements ITableScopedLocalizedAssetProvider + reader seam
│   │   ├── IStringTableReader.cs / IAssetTableReader.cs   # NEW internal seams (R5)
│   │   └── AssemblyInfo.cs                       # NEW: InternalsVisibleTo the new test assembly
│   └── Editor/
│       ├── UnityLocalizationSyncer.cs            # per-group collections, carry-over pre-pass, internal root overload
│       └── Batch/TranslationImportBatch.cs       # key-routed -speakersCsv, shared naming
└── Tests/
    ├── EditMode/                                 # + LocalizationTableNames, TableScopedLookup, LocalizationCsv,
    │                                             #   database grouping, BuildCsv carry, GlobalKeyCsvRouter tests
    └── Localization.Unity.EditMode/              # NEW assembly (defineConstraints GRAPHLOCALIZATION_UNITY_LOCALIZATION):
                                                  #   provider targeting (fake readers), syncer grouping + carry-over

com.faolline.graphdialoguesystem/
├── Runtime/Speakers/Speaker.cs                   # + _localizationGroup / LocalizationGroup
├── Runtime/Localization/DialogueLocalizationKeys.cs   # + LibName, SpeakerTableGroup, ForSpeakerTable, ForGraphTable
├── Runtime/Playback/DialoguePresenter.cs         # owner-graph overloads; targeted line/choice/voice/speaker lookups
├── Runtime/Playback/DialoguePlayer.cs            # passes _runner.CurrentGraph
├── Editor/Inspector/SpeakerEditor.cs             # "Localization Group" field + resolved table label
├── Editor/Localization/DialogueGraphLocalizationAdapter.cs  # grouped global keys, sorted, conflict error
├── Samples~/GameFlowDialogueBridge/GraphFlowDialogueSource.cs  # passes _driver.Runner.CurrentGraph
└── Tests/EditMode/…                              # presenter/player targeting, adapter grouping/conflict

com.faolline.graphquest/
├── Runtime/QuestEvaluator.cs                     # ResolveWithFallback → TableScopedLookup with ForGraph(_quest.name)
└── Tests/EditMode/QuestLocalizationTests.cs      # + targeted-table assertions

com.faolline.graphimport/
├── Runtime/SpeakerGroups/SpeakerGroupMapping.cs  # NEW: pure parse + validation (+ small internal RFC4180 reader)
├── Runtime/SpeakerGroups/SpeakerGroupMappingException.cs   # NEW
├── Editor/Resolution/ProjectAssetResolver.cs     # optional mapping → group on create
├── Editor/Resolution/SpeakerGroupApplier.cs      # NEW: align existing referenced speakers, report changes
├── Editor/Batch/DialogueImportBatch.cs           # -speakerTablesCsv
├── Editor/Window/GraphImportWindow.cs            # "Speaker tables CSV" field
└── Tests/EditMode/…                              # mapping parse, resolver create-with-group, applier

com.faolline.graphTest/Tests/EditMode/Architecture/DependencyMatrixTests.cs   # register new test assembly
ARCHITECTURE.md                                                              # same
```

**Structure Decision**: All changes stay inside the four existing packages plus the architecture guard. Pure logic goes into Runtime/Editor classes with no Unity Localization dependency, so it is testable in the existing `com.faolline.graphlocalization.Tests.EditMode`. Only code that needs `com.unity.localization` types lives in, and is tested from, the gated `Localization.Unity` assemblies.

## Complexity Tracking

| Deviation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Optional companion interfaces (`ITableScoped…Provider`) + `TableScopedLookup` dispatch helper | FR-011/SC-006: consumer providers must keep compiling and working | Adding `ResolveInTable` to `ILocalizationProvider` breaks every consumer implementation. A C# default interface member is avoided because its runtime support across Unity scripting backends is not guaranteed for this ecosystem's supported range. |
| Internal table-reader seams in the Unity providers + `InternalsVisibleTo` | SC-001/SC-002 ("opens 0 other tables") must be *verified*. Unity Localization's runtime table database is not reliably drivable from EditMode tests. | Testing only the callers with a fake provider proves the *right table is asked for*, but not that the Unity provider *stops probing*, which is the actual regression being fixed. |
| Small RFC4180 reader duplicated inside `graphimport` Runtime | Mapping values may be quoted (commas in group names, Excel exports) | `graphimport.Runtime` is `noEngineReferences: true` and cannot reference `graphlocalization.Runtime` (engine-dependent). Moving the parser to graphimport Editor would still need a new asmdef edge to graphlocalization just for ~40 lines. |
| Unknown-table fallback to probing (R4) | A graph whose runtime name no longer matches a built collection (an `Instantiate`d clone named `X(Clone)`, a code-built graph never built into tables) must not regress from "found by probing" to "missing" | Strict "designated table or nothing" would silently turn today's working lookups into missing texts for those cases. The fallback only triggers when the table is **absent from the manifest**. A *known* table that lacks the key is still a plain missing text (FR-009). |
