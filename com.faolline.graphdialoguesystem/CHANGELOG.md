# Changelog

All notable changes to **com.faolline.graphdialoguesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.5.2]

### Changed
- **Dialogue editor: a language picker instead of a free-text locale field.** The toolbar's Run-locale control is
  now a dropdown of the project's configured languages — the Unity Localization locales, or the CSV locale
  columns — sourced from graphlocalization's `LocalizationLocaleCatalog`, instead of a free-text code you had to
  type correctly. The list is read when the toolbar is built (reopen the window to pick up newly added locales).
  Requires graphlocalization ≥ 0.4.0.

## [0.5.1]

### Fixed
- **Dialogue node inspector no longer overlaps itself.** Selecting a node painted the no-selection panel (Speakers +
  Parameters) UNDER the node's own sections (Line / Choice / SubDialogue / Node Properties). `BindNode` now clears
  the panel without rebuilding the no-selection content (that shows only when nothing is selected). Pairs with the
  graphcore 0.13.1 fix (the inspector panel scrolls instead of compressing when a node has many fields).

## [0.5.0]

### Added
- **Fluent code-first dialogue builder** (`DialogueGraphBuilder`) — the dialogue counterpart of graphstandard's
  `GraphBuilder` (which only makes universal nodes, so a plain statement is silently drained instead of spoken).
  Build dialogues directly: `AddLine(speaker).Say(text)`, `AddChoice()` + `.Option(label).To(target)`, `AddEnd`,
  `To`/`AsEntry`/`Id`/`When`/`WithSpeaker`, then `Build()` → a `DialogueGraph`. The right node types
  (`DialogueLineNodeData`, `ChoiceNodeData` + `DialogueChoice`) and their `NodeType` ids are set for you, so a
  built dialogue plays with no hand-assembly (round-5 findings #1 + #5).
- **Table-less rendering** (`DialogueTitleProvider.FromGraph(graph)`) — an `ILocalizationProvider` that resolves
  a dialogue's derived line/choice keys to their authored `Title`, so a code-built dialogue renders its actual
  text with NO CSV / localization table (otherwise a key with no table entry shows the bare `#line_<guid>`
  marker). The "just show what I authored" path for prototyping/tests (round-5 finding #2).

### Notes
- Additive (MINOR); graphcore untouched. Resolves the round-5 "code-first dialogue is heavy" friction. 3 EditMode
  tests. `DialogueDriver`'s serialized `graph` field stays — it is the standalone path (the host-embedded path
  uses a SubGraph + `DialoguePresenter`, no driver), so no removal.

## [0.4.0]

### Added
- **`DialoguePresenter` opt-in `titleFallback`** (ctor, default `false`) — when a localization key is missing,
  fall back to the node/choice authored `Title` (the source text the localization pipeline derives its source
  column from) instead of the bare `#key` marker. Useful before a table is exported or for an incomplete locale.
  Strict mode still throws; Audit still records the key. `DialoguePlayer` keeps the default (no behavior change).

### Notes
- Additive (MINOR); graphcore untouched. Round-7 refinement (restores the Title fallback the round-6 hand-rolled
  resolution had, which the presenter had dropped).

## [0.3.0]

### Added
- **`DialoguePresenter`** (Runtime/Playback) — runner-agnostic resolution of dialogue nodes into displayable
  steps. Given a `DialogueLineNodeData`/`ChoiceNodeData` + a `BaseContext` + the providers, it produces the
  same `LineStep`/`ChoiceStep` the player emits, for a node owned by **any** runner. This lets a host (e.g. a
  gameflow `GraphFlowDriver` that embeds a dialogue **subgraph**) *render* dialogue without owning a
  `DialoguePlayer` — removing the ~40-line resolution rewrite a round-6 consumer hit. `Resolve(node, ctx)`
  returns `null` for a non-dialogue node; `MissingKeys`/`OnMissingKey` and strict modes work as in the player.

### Changed
- `DialoguePlayer` now resolves through an internal `DialoguePresenter` — **public API and behavior unchanged**
  (the existing playback suite is the regression guard).
- README version header corrected (was stale at `0.1.0`).

### Notes
- Additive (MINOR); graphcore untouched; no dependency on gameflow (the consumer composes the host runner +
  the presenter, per Constitution VII).

## [0.2.0]

### Added
- Headless dialogue runtime: `DialoguePlayer` over graphcore's runner — localized `LineStep` /
  `ChoiceStep` / `EndStep`, `Advance` / `Choose` / `Back` / `BackToCheckpoint`, save/restore,
  `OnStuck`, missing-key audit.
- Authoring: `DialogueGraph` (owns its **Speakers**), `DialogueLineNodeData`, `DialogueChoice`,
  `Speaker` (+ expressions, name color), inline conditions/effects, derived localization keys.
- In-game UI (`com.faolline.graphdialoguesystem.UI`): `IDialogueView`, Canvas + UI Toolkit views,
  `DialogueDriver` (Space / 1–9, new + legacy input), avatar lifecycle.
- **Typewriter** reveal (+ skip), **auto-advance**, **timed choices**, per-speaker **name color**.
- `{key}` **text interpolation** from the context blackboard (after localization).
- **Localized line audio** via Unity Localization Asset Tables (resolved by the line's key, per locale).
- **Backlog/history**: driver `History` + `OnLineShown`; `CanvasDialogueBacklog`.
- Editor: dialogue graph window (Run/Choose/Continue/Back/Validate), speaker/expression dropdowns,
  custom `Speaker` inspector, sample generator.

### Notes
- Depends on `com.faolline.graphcore` and `com.faolline.graphlocalization`.
- Deferred: per-speaker portrait side (needs a left/right avatar-mount redesign).
