# Tasks: Graph texts kept in step with their localization tables

**Tests**: REQUIRED (TDD). New members are stubbed first so a red run fails on assertions, not on compile errors.

**Test runner** (dev project Editor closed): `"C:/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe" -batchmode -projectPath H:/repos/Perso/LIB_Unity/FaollineGraphEcosystem -runTests -testPlatform EditMode [-testFilter <regex>] -testResults <xml> -logFile <log>`, **without** `-quit`.

## Phase 1: Setup

- [X] T001 Baseline: full EditMode suite on the branch HEAD → `../../Logs/054-baseline.xml` (logs removed after the run; results in Notes)

## Phase 2: graphlocalization — detection (US1)

- [X] T002 Stub `GraphTextDrift`, `IGraphTextWriter`, `GraphTextSync` (Compare / FormatReport / Apply / ApplyAll), `LocalizationDatabase.Tables`, `CsvLocalizationExporter.ReadSourceTexts`, `UnityLocalizationSyncer.ReadSourceTexts`
- [X] T003 Tests `GraphTextSyncCompareTests` (equal, differ, whitespace/CRLF, empty cell, missing key, empty graph text, global keys with group table, graph GUID/name/table filled)
- [X] T004 Tests `LocalizationDatabaseTablesTests` (graph tables then group tables, same names as the build)
- [X] T005 Tests `CsvSourceTextsTests` (reads the first-locale column from each key's own file, ignores other files)
- [X] T006 Tests `UnityLocalizationSourceTextsTests` (reads the source-locale entry of the desired collection only)
- [X] T007 Tests `GraphTextSyncApplyTests` (routes to the lib's writer, global vs graph, no writer → not applied, count)
- [X] T008 Red run → implement T002 members → green; refactor exporter/syncer onto `Tables`; existing suites green
- [X] T009 Build hook in `LocalizationBuilderCore` + menus

## Phase 3: write-back per lib (US2)

- [X] T010 Tests `DialogueGraphTextWriterTests` (line Title, choice Title, unknown key → false, speaker by key via assets)
- [X] T011 Tests `QuestGraphTextWriterTests` (quest name/description, objective title/description, unknown → false)
- [X] T012 Red → implement the adapter hooks → green

## Phase 4: documentation (US3) and release

- [X] T013 READMEs (graphdialoguesystem, graphquest, graphlocalization): table is the reference, seed wording, menus
- [X] T014 CHANGELOGs + versions (gl 0.11.0, gds 0.22.0, gq 0.13.0), floors, version references across the ecosystem
- [X] T015 Full suite headless, compare with baseline
- [X] T016 Hand over for review (reviewed by the user, then committed)

## Notes

- Baseline (T001): 1542/1542. Red run (T008): 39/42 failed on `NotImplementedException` (the 3 DependencyMatrix tests passed). Final full suite (T015): **1581/1581** (+39).
- End-to-end probe on the dev project (CSV mode, throwaway `-executeMethod` script, removed afterwards; files restored from a backup and diffed): 0 drift → edit `SampleDialogue.csv` `line_intro` → 1 drift; `BuildAll` logged the warning and kept the edited cell; `ApplyAll` → `SampleDialogue.asset` Title updated → 0 drift. The manifest did not change.
- The graphquest test assembly now references `com.faolline.graphlocalization.Editor` (DependencyMatrixTests updated; same tier as the dialogue test assembly).
- graphimport's floor on graphdialoguesystem stays at 0.21.0: it uses none of the new API.
