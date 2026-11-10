# Implementation Plan: Graph texts kept in step with their localization tables

**Branch**: `054-graph-text-table-sync` | **Date**: 2026-11-10 | **Spec**: [spec.md](spec.md)

## Summary

`com.faolline.graphlocalization` learns to read back the source-language cell of every key it builds and to compare
it with the graph text its adapter reported. Mismatches are reported at the end of each build and on demand; a
separate, explicit action copies the table text back into the graphs through each lib's adapter.

## Technical Context

**Language/Version**: C# (Unity 6000.0 floor; dev project on 6000.3.20f1)

**Packages**:
- `com.faolline.graphlocalization` 0.10.0 → **0.11.0** (MINOR, new editor API):
  - `LocalizationDatabase.Tables(libName)`: the (table, keys, graph) list the build writes. The CSV exporter and
    the Unity syncer use it too, so "which table holds a key" has one definition.
  - `GraphTextDrift` (lib, graph GUID/name, table, key, type, node id, graph text, table text).
  - `GraphTextSync`: `Compare` (pure), `ReadTableTexts` (backend dispatch), `FindDrifts`, `Apply`, `ApplyAll`,
    `FormatReport`, the two menus.
  - `IGraphTextWriter`; `BaseGraphLocalizationAdapter<TGraph>` implements it with two virtual hooks
    (`ApplyGraphText(TGraph, drift)`, `ApplyGlobalText(drift)`, both default `false`) and a public
    `WriteGraphText(TGraph, drift)` overload for in-memory graphs.
  - `CsvLocalizationExporter.ReadSourceTexts(...)`, `UnityLocalizationSyncer.ReadSourceTexts(...)` (the latter
    called through reflection, like `SyncDatabase`).
  - `LocalizationBuilderCore`: after each lib's export, compare and log the report.
- `com.faolline.graphdialoguesystem` 0.21.0 → **0.22.0**: adapter writes line/choice `Title` and speaker
  `DisplayNameFallback`. Floor graphlocalization → 0.11.0. README rewording.
- `com.faolline.graphquest` 0.12.1 → **0.13.0**: adapter writes quest `DisplayName`/`Description` and objective
  `Title`/`Description`. Floor graphlocalization → 0.11.0. README note.
- Other packages flooring graphlocalization: re-check (floor drift rule), raise only if they adopt the new API.

**Testing**: EditMode, headless batchmode (dev project Editor closed), TDD: stubs first, red on assertions, then
green. Targeted filters per package, full suite before review.

## Design notes

- Comparison lives on `LocalizationKeyEntry.DefaultHint`, the exact value the build pre-fills with, so detection
  follows each adapter's own fallback rules (e.g. objective hint = `Id` when `Title` is empty).
- Unity backend read: the desired collection only (`LocalizationTableNames.TextCollection(table)`), source locale
  resolved by the syncer's existing `GetSourceLocale`. No collection → no entries → no mismatch.
- CSV read: `{CsvOutputFolder}/{lib}/{table}.csv`, column = first CSV locale, parsed with `LocalizationCsv`.
- The build reads right after it wrote, so empty cells it just filled are equal and never reported.
- Writers record Undo and mark dirty; `Apply`/`ApplyAll` save assets once at the end (optional `save` flag).
  Saving a graph triggers the auto-build, which then finds no mismatch.

## Constitution Check

- II (core knows no domain): graphlocalization stays domain-free; the write-back is in each lib's adapter. ✅
- IV (tests first): every behavior has a failing test before code. ✅
- No new assembly, no new asmdef edge, no external dependency. ✅
