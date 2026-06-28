# Changelog

All notable changes to **com.faolline.graphdialoguesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.10.0]

### Changed
- **`DialoguePlayer` and `DialogueBus` accept any `BaseContext`** (not just `DialogueContext`). A
  `GameFlowContext` or any host context is now used directly — no more silent fork to an empty
  `DialogueContext`. Interpolation `{var}`, conditions, and writes flow through the shared context.
  `DialogueContext` remains available as a typed convenience subclass but is no longer required.
- **Warning at drain ceiling.** When the pass-through drain loop hits `MaxDrainSteps` (1000), a
  `[GraphDialogue]` warning is logged instead of exiting silently — helps diagnose runaway chains or
  undetected cycles.

## [0.9.0]

### Added
- **`PlayDialogueAction`** — a graphcore `BaseAction` that launches a dialogue from a gameflow/quest node. The
  host's runner stays parked while the dialogue plays; on end, context state written by the dialogue is visible
  to the host. Requires graphcore 0.20.0.
- **`DialoguePlayer` signal and time support.** The player now forwards `RaiseSignal` and `Tick(dt)` to its
  internal runner, so dialogue nodes can await signals and use timed waits.
- **Edge condition fluent helpers** on `DialogueGraphBuilder` — `.When(condition)` on an edge, parity with
  graphstandard's builder.
- **Dialogue handle parity** with graphstandard (same `IGraphHandle` surface for host interop).

### Changed
- **Ecosystem-wide documentation pass** — tooltips, headers, `[HelpURL]` on key types, README updates.

## [0.8.0]

### Added
- **Auto-wire localization from `LocalizationContext`.** When a `LocalizationContext` is present the dialogue
  system automatically resolves its localization provider — no manual wiring needed.
- **`OutcomeLabel` on dialogue End nodes.** Inherits graphcore's semantic outcome label; the host can branch on
  how the dialogue ended.
- **Per-node `LocalizedAssetFlags` support.** Dialogue nodes respect the per-node flags for asset table filtering
  (Text, Audio, etc.), following graphcore 0.19.0.
- **Double-click SubGraph/SubDialogue nodes** to open the target dialogue in the Dialogue editor.

### Changed
- **All inspectors route through `BuildNoSelectionContent`** for consistent panel behavior.
- **`NodeId` populated on `LocalizationKeyEntry`** so the localization adapter correctly links keys to their
  source nodes.

## [0.7.3]

### Changed
- **Dialogue editor registers for GraphLink navigation.** `DialogueGraphEditorWindow` now registers itself with
  `GraphEditorWindowRegistry` (and exposes `Open(DialogueGraph)`), so double-clicking a `GraphLinkNodeData` that
  targets a `DialogueGraph` opens it in the Dialogue editor. Dependency floor: graphcore `0.17.0` → `0.18.0`.

## [0.7.2]

### Added
- **`DialoguePlayer` now accepts `titleFallback`** (optional ctor arg, default false), forwarded to its internal
  `DialoguePresenter`. Previously only `DialoguePresenter` could opt in, so a standalone code-built dialogue with
  no CSV rendered `#line_<id>` markers — now the standalone path can fall back to the authored node Title too.
  Resolves the player/presenter asymmetry surfaced by the Cryptique rebuild.

## [0.7.1]

### Changed
- **`DialoguePlayer.Choose` / `Advance` now log a diagnostic instead of failing silently** when called in the
  wrong state. The player pauses on BOTH line and choice nodes; the common mistake is calling `Choose(id)` while
  still paused on a LINE (you must `Advance()` past the line to reach the choice first) — that used to be a silent
  no-op with no feedback. Each ignored case (`Choose` off a choice, an unknown/unavailable option, `Advance` off a
  line) now warns with `[GraphDialogue] …`. Behaviour is otherwise unchanged. Surfaced by a dogfood that drove
  `Start → Choose` with no `Advance` and saw nothing happen.

## [0.7.0]

### Removed (breaking)
- **The back-compat primitive subclasses are gone.** `BoolCondition`, `IntCondition`, `FloatCondition`,
  `StringCondition`, `AlwaysTrueCondition`, `AlwaysFalseCondition`, `SetBoolAction`, `SetIntAction`,
  `SetFloatAction`, `SetStringAction`, `LogAction` (the thin subclasses kept in 0.5.6 / 0.6.0) were removed now
  that the canonical implementations live in GraphCore. **Use `Faolline.GraphCore.*` directly.** This fully
  eliminates the CS0104 ambiguity with `GraphStandard.*`. Existing assets typed as the old `GraphDialogue.*`
  primitives must be re-pointed to the GraphCore type. (Dialogue-specific nodes — lines, speakers, choices,
  inline effects — are unaffected.)

### Changed
- **The primitive condition/action set now subclasses the canonical GraphCore types** (following the bool pair in
  0.5.6): `IntCondition`, `FloatCondition`, `StringCondition`, `AlwaysTrueCondition`, `AlwaysFalseCondition`,
  `SetIntAction`, `SetFloatAction`, `SetStringAction`, `LogAction` are now thin back-compat subclasses of
  `Faolline.GraphCore.*`. Existing `GraphDialogue/…` assets and code keep working; new graphs should prefer the
  GraphCore types. Resolves the remaining CS0104 ambiguities with `GraphStandard.*` for consumers importing both.
- **Behaviour note:** `IntCondition`, `FloatCondition` and `StringCondition` now read an absent key as `false`
  **silently** by default (the canonical GraphCore behaviour), instead of always warning. Set `WarnOnMissing = true`
  to restore the warning.

### Removed (source-breaking)
- **`Faolline.GraphDialogue.ComparisonOperator` was removed** — it is now `Faolline.GraphCore.ComparisonOperator`
  (same names/values, so serialized enum values in existing assets are unaffected). Source that referenced the
  enum by its old namespace must switch to `Faolline.GraphCore.ComparisonOperator`. Requires graphcore `0.17.0`.

### Changed
- **`BoolCondition` / `SetBoolAction` now subclass the canonical GraphCore types** (implementation hoisted to
  `Faolline.GraphCore.BoolCondition` / `SetBoolAction`). Existing `GraphDialogue/…` assets and code keep working;
  new graphs should prefer the GraphCore types. Resolves the CS0104 ambiguity with `GraphStandard.*` for consumers
  importing both namespaces. Requires graphcore `0.16.0`.
- **Behaviour note:** `BoolCondition` now reads an absent key as `false` **silently** by default (the canonical
  GraphCore behaviour), instead of always logging a warning. Set `WarnOnMissing = true` to restore the warning.
  (`IntCondition` and the other dialogue conditions are unchanged.)

### Changed
- **Only one Start node per dialogue.** Adopts graphcore's shared Add-Start action: the "Add Start Node"
  context-menu item is greyed out once the graph has a Start (a graph has a single entry point), and a second
  Start is refused even programmatically. Requires graphcore ≥ 0.14.0.

## [0.5.4]

### Changed
- **Toolbar grouped by usage.** The dialogue editor toolbar no longer crams every control into one row — it now
  reads as groups separated by thin dividers: document tools (Save / Arrange / ↻ Refresh) │ playback (Run / Choose
  / ▶ Continue / ← GoBack / ⏮ Checkpoint) │ ✓ Validate, with the Run-language picker moved to the right as a
  setting (via the new graphcore `PopulateToolbarRight` hook + `ToolbarSeparator`). Requires graphcore ≥ 0.13.3.

## [0.5.3]

### Changed
- **Language picker reloads on Save / ↻ Refresh.** The toolbar's language dropdown now rebuilds its list from the
  localization settings whenever the window is saved or the new graphcore ↻ Refresh button is pressed (via an
  `OnRefresh` override), so locales added after the window opened show up without reopening it. Requires
  graphcore ≥ 0.13.2.

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
