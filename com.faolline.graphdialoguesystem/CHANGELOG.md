# Changelog

All notable changes to **com.faolline.graphdialoguesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.1.0]

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
