# Feature Specification: Graph texts kept in step with their localization tables

**Feature Branch**: `054-graph-text-table-sync`
**Created**: 2026-11-10
**Status**: Implemented (D1–D4 accepted at review)
**Input**: an issue raised on a consumer project: a dialogue line edited in its graph node kept showing the old text in game.

## Context

The table build fills empty cells only. It never overwrites a value already in a table. This is intended: **the
translation table is the reference** for every text. The text typed in a graph (a line's or choice's `Title`, a
speaker's `Display Name Fallback`, a quest's `Display Name`/`Description`, an objective's `Title`/`Description`) is
only the seed copied into the source-language cell the first time the key is built.

The consumer edited a line in its node after the first build. The table kept the old text, the game showed the old
text, and nothing reported it. Two gaps made this easy:

1. The READMEs call the graph text "the source text the table is derived from", which reads as "the graph is the
   reference".
2. Nothing reports a graph text that no longer matches its table. Once the table is edited (the intended
   workflow), the graph view still shows the old text, and `titleFallback` falls back to it.

## User Scenarios & Testing

### User Story 1 — See which graph texts no longer match their table (P1)

As an author, after a table build or on demand, I learn which graph texts differ from the source-language cell of
their table, so I know the graph view is out of date (or that I edited the wrong place).

**Acceptance**:
1. Given a line whose `Title` differs from its non-empty source cell, when the tables are built, then one warning
   for the lib reports the mismatch count and names the first mismatches (graph, key, both texts), and says the
   table is the reference and how to update the graphs.
2. Given a `Title` equal to the cell except for surrounding whitespace or line-ending style, no mismatch is reported.
3. Given an empty source cell (not built yet), no mismatch is reported: the build fills it from the graph.
4. Given an empty `Title` and a filled cell (text imported straight into the table), a mismatch is reported.
5. Speaker names (global, per group table) and quest/objective texts are checked the same way.
6. Both backends (CSV and Unity Localization) are read, from the same table the build writes the key to.
7. **Faolline ▸ Localization ▸ Check Graph Texts Against Tables** reports the same thing without building.

### User Story 2 — Update the graphs from the tables (P1)

As an author, I copy the table's source text back into the graph texts in one action, so the graph view matches
what the game shows.

**Acceptance**:
1. **Faolline ▸ Localization ▸ Update Graph Texts From Tables** writes each mismatched cell's text into the graph
   text it came from (line/choice `Title`, speaker `Display Name Fallback`, quest `Display Name`/`Description`,
   objective `Title`/`Description`), with Undo, and saves the assets.
2. A public API (`GraphTextSync`) exposes find / apply-one / apply-all, so a consumer validator can offer the same
   fix per finding.
3. Nothing is ever written automatically: the build only reports.
4. A mismatch whose lib has no writer is left alone and reported as not applied.

### User Story 3 — Documentation states the rule (P1)

The graphdialoguesystem, graphquest and graphlocalization READMEs say plainly: the table is the reference; the
graph text is a seed copied on the first build only; edit texts in the table afterwards; use the check/update menus
to bring graphs back in step.

## Requirements

- **FR-001** Detection compares, per key, the graph text the adapter reports (`LocalizationKeyEntry.DefaultHint`)
  with the source-language cell of the table the build files that key under.
- **FR-002** Source language = the one the build pre-fills: Unity backend = the settings' Unity Source Locale, else
  the Project Locale, else the first locale; CSV backend = the first CSV locale.
- **FR-003** Comparison ignores leading/trailing whitespace and `\r\n` vs `\n`. Empty cell = no mismatch.
- **FR-004** Detection never writes anything. The build logs one warning per lib with mismatches (category
  `GraphLocalization.Validation`), listing at most a few entries.
- **FR-005** Applying writes the cell's text (line endings normalized to `\n`) into the graph text, through the
  lib's adapter, with Undo; graph-level texts by graph asset GUID + key, global texts (speakers) by key.
- **FR-006** Generic in `com.faolline.graphlocalization`: no domain type is known there. Libs supply the write-back
  through `BaseGraphLocalizationAdapter<TGraph>` hooks or the `IGraphTextWriter` interface.
- **FR-007** Existing build output is unchanged (tables, CSV files, manifest).

## Decisions taken without the user (to confirm at review)

- **D1** The build reports mismatches as a **warning** (one per lib), never an error, whatever `LocaleValidation`
  is. Toggleable through the graphlogging category.
- **D2** One direction only for the fix: **table → graph**. No "graph → table" action (the table is the reference).
- **D3** Scope = dialogue (lines, choices, speakers) **and quests** (same build, same trap).
- **D4** Names: `GraphTextDrift`, `GraphTextSync`, `IGraphTextWriter`, menus *Check Graph Texts Against Tables* /
  *Update Graph Texts From Tables*.

## Out of scope

- Detecting translations (other locales) made from an older source text.
- Showing the table text in the graph view instead of the `Title`.
