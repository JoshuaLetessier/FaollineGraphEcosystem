# Changelog

All notable changes to **com.faolline.graphdialoguesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
