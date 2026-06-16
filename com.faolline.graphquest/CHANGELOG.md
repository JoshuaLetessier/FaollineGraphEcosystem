# Changelog

All notable changes to **com.faolline.graphquest** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.1.3]

### Changed
- **Documented the k-of-N quest-completion trap on `ObjectiveBuilder.RequiresAtLeast`.** A quest only reaches
  `Completed` when ALL its non-optional objectives are complete, so for a "do k of N" quest the N counted
  sub-objectives must be marked `Optional()` (with one required join objective carrying `RequiresAtLeast(k, …)`)
  — otherwise the quest waits for all N. Also notes that `count` greater than the prerequisite count leaves the
  join permanently Locked. Doc-only; surfaced by a dogfood that modelled the N relics as required objectives.

## [0.1.2]

### Changed
- **Sample now builds the canonical GraphCore primitive nodes.** `QuestSampleBuilder` (and the regenerated
  `SampleQuest.asset`) use `Faolline.GraphCore.BoolCondition` / `SetBoolAction` directly, following the removal of
  the `GraphStandard.*` back-compat subclasses. Dependency floors bumped: graphcore `0.14.0` → `0.17.0`,
  graphstandard `0.10.1` → `0.12.0`. No runtime/API change to the quest library itself.

## [0.1.1]

### Added
- **Entry / terminal objective cues in the editor.** A quest has no Start/End node (it is a reactive objective
  DAG — no traversal cursor), so the Quest Graph editor now marks each objective that is an **entry** (no
  prerequisite) or **terminal** (nothing depends on it) right on the node, so the begin/end of the DAG reads at a
  glance (cues refresh on Save / ↻ Refresh after edges change). New `QuestGraphView.HasPrerequisite` /
  `HasDependent` topology helpers; the README documents why there is no Start/End. +1 EditMode test.

## [0.1.0]

### Added
- **Initial release — quests & objectives derived from the shared context.** A domain library above graphcore +
  graphstandard. Quests (`QuestGraph`) hold objectives (`ObjectiveNodeData`) whose states
  (`Locked`/`Active`/`Completed`/`Failed`) are derived from a `BaseContext`:
  - **Prerequisite gating** (linear chains AND DAGs) delegated to graphstandard's `ReactiveEvaluator` — objectives
    are nodes, prerequisites are edges; "back" is a re-pass, not an undo.
  - **Completion / fail conditions** are graphcore `BaseCondition`s; **rewards** are graphcore `BaseAction`s fired
    **exactly once** on the completed transition (guarded by a context "rewarded" set). No new condition language.
  - **Code-first fluent builder** (`QuestBuilder` / `ObjectiveBuilder`); cyclic prerequisites are rejected at
    `Build()` with a `[GraphQuest]` diagnostic. Prerequisites are all-of-N (`Requires`) or **k-of-N**
    (`RequiresAtLeast(k, …)`) — the latter surfaces graphstandard's `ReactiveEvaluator` k-of-N gating, so a
    "do 2 of these 3" gate needs no synthetic counter objective.
  - **`QuestEvaluator.Reset()`** clears a quest's progress for replay (its scoped completed/failed/rewarded sets
    only — other quests sharing the context are untouched), so one-shot rewards can fire again.
  - **Journal data layer** for a consumer quest-log UI: per-objective `DisplayName` (via `.Named(...)`, backed by
    `BaseNodeData.Title`) and `Description` (`.Describe(...)`), quest-level `DisplayName`/`Description`, and
    `QuestEvaluator.GetObjectives()` → `ObjectiveView` snapshots (id + label + description + required + state) plus
    `RequiredCompleted`/`RequiredTotal` progress — so the consumer needs no id→label table of its own. The library
    ships the data; the in-game UI stays consumer territory.
  - **Localized journal text.** `QuestEvaluator.UseLocalization(ILocalizationProvider)` treats objective/quest
    `DisplayName` + `Description` as localization KEYS resolved through the provider's current locale (else they
    stay literal). Uses graphlocalization's neutral abstraction (the CSV provider works without Unity
    Localization). Adds a `com.faolline.graphlocalization` 0.4.0 dependency.
  - **Quest-level** unlock condition + completion reward + `AllRequired` completion rule.
  - **Time-limited objectives.** `ObjectiveBuilder.WithTimeLimit(seconds)` + `ObjectiveNodeData.TimeLimitSeconds`:
    an Active objective Fails if not Completed within the limit. Enforced only when the host ticks
    `QuestEvaluator.Evaluate(now)` with a game clock (`Evaluate()` ignores timers); completing on the deadline tick
    still wins. `GetRemainingSeconds(id)` powers a countdown UI; the deadline is a context param (persists/reverts;
    `Reset()` re-arms it).
  - **Cross-quest chaining.** Each evaluator syncs its quest's id in/out of a shared `CompletedQuests` context set
    as it completes/un-completes; a quest can gate its unlock on other quests via `UnlockAfter(...questIds)` (or
    `QuestCompletedCondition.For(...)`). Derived from the context, so it persists through graphsave and reverts on a
    context revert; evaluate prerequisite quests before the ones chained after them.
  - **Persistence for free**: all state lives in `BaseContext` collections, so a `com.faolline.graphsave` context
    snapshot round-trips quest progress with no quest-specific snapshot type and **no runtime graphsave
    dependency**.
  - **Host seam**: `QuestEvaluator` runs against any `BaseContext` (e.g. a gameflow host's `GameFlowContext`) with
    **no dependency on gameflow**.
  - **Visual quest editor** (`com.faolline.graphquest.Editor`): a `QuestGraphEditorWindow` (open via
    `Faolline ▸ Open Quest Graph Editor` or by double-clicking a `QuestGraph` asset) with objective nodes, drawable
    prerequisite edges (From→To = "To requires From"), and an inspector for objective fields (display name,
    description, required, k-of-N prereqs, time limit, completion/fail conditions, reward) and quest-level metadata
    including the **Quest Id** (the stable id `UnlockAfter` / `QuestCompletedCondition` reference — so cross-quest
    chaining is fully authorable in the window). `QuestCompletedCondition` has a `[CreateAssetMenu]`
    (`Create ▸ GraphQuest ▸ Conditions ▸ Quest Completed`) so the chaining condition is creatable as an asset.
    `QuestGraph` has a `[CreateAssetMenu]`
    (`Create ▸ GraphQuest ▸ Quest Graph`), and `Faolline ▸ GraphQuest ▸ Create Sample Quest` builds a ready-made
    "The Keep Escape" sample asset (chain + optional objective + rewards, all sub-assets). An editor-authored quest
    with no `QuestId` scopes its state by the graph's stable `GraphId`.
- Dependencies: `com.faolline.graphcore` 0.14.0, `com.faolline.graphstandard` 0.10.1.

### Notes
- v1 is code-first; the visual quest editor and an in-game quest journal/tracker UI are deferred (libs ship data +
  seams only). EditMode-tested.
