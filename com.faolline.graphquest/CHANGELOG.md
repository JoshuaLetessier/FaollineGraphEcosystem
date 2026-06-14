# Changelog

All notable changes to **com.faolline.graphquest** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
    `Build()` with a `[GraphQuest]` diagnostic.
  - **Quest-level** unlock condition + completion reward + `AllRequired` completion rule.
  - **Persistence for free**: all state lives in `BaseContext` collections, so a `com.faolline.graphsave` context
    snapshot round-trips quest progress with no quest-specific snapshot type and **no runtime graphsave
    dependency**.
  - **Host seam**: `QuestEvaluator` runs against any `BaseContext` (e.g. a gameflow host's `GameFlowContext`) with
    **no dependency on gameflow**.
- Dependencies: `com.faolline.graphcore` 0.14.0, `com.faolline.graphstandard` 0.10.1.

### Notes
- v1 is code-first; the visual quest editor and an in-game quest journal/tracker UI are deferred (libs ship data +
  seams only). EditMode-tested.
