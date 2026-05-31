# Implementation Plan: graphdialoguesystem — Graph-Based Dialogue Library (MVP)

**Branch**: `010-graphdialoguesystem-mvp` | **Date**: 2026-05-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/010-graphdialoguesystem-mvp/spec.md`

## Summary

Create `com.faolline.graphdialoguesystem`: a dialogue-domain library built **only** on
`com.faolline.graphcore` (v0.2.0), mirroring the validated `com.faolline.starterGraph` package shape.
It adds the dialogue layer — spoken lines, speakers, localized text, choices with localized labels —
on top of graphcore, which already provides the canvas, node types, deterministic persistence,
serialization, node search, the inspector framework, validation, the typed blackboard (`BaseContext`),
and the headless playback engine (`BaseRunner`). **Zero graphcore changes.**

The library subclasses graphcore where it needs domain data (`DialogueGraph : BaseGraph`,
`DialogueLineNodeData : StatementNodeData`, `DialogueChoice : BaseChoice`, `DialogueContext :
BaseContext`) and reuses the built-in `StartNodeData`/`ChoiceNodeData`/`EndNodeData`/`SubGraphNodeData`
unchanged. Reactivity is **inline only** (conditions on choices/edges/entry, effects on enter/exit) —
no condition/effect node types. Localization goes through a lib-owned `ILocalizationProvider`
abstraction with a self-contained CSV provider by default and an **optional, isolated** Unity
Localization adapter (separate assembly, compiled only when the package is present), per constitution
v1.2.0. A `DialoguePlayer` wraps `BaseRunner` and emits localized line/choice/end steps for a game UI.

## Technical Context

**Language/Version**: C# 9 (Unity 6000.0.x)

**Primary Dependencies**: `com.faolline.graphcore` (Runtime + Editor). Optional, isolated:
`com.unity.localization` (adapter assembly only).

**Storage**: Unity `ScriptableObject` assets. `DialogueGraph` is a `BaseGraph`; nodes via
`[SerializeReference]`, choices via `[SerializeReference]`, conditions/actions are
`BaseCondition`/`BaseAction` `ScriptableObject`s. CSV localization tables are plain text assets.

**Testing**: NUnit via Unity Test Runner — **EditMode only**, headless.

**Target Platform**: Unity Editor (authoring) + headless runtime (playback).

**Project Type**: New downstream Unity package with Runtime / Editor / optional-adapter / Tests
assemblies.

**Performance Goals**: Inspector edits reflect within one frame; a step (advance/choose) resolves in
well under one second for human-scale dialogues; graph load is linear in nodes+edges.

**Constraints**: No `MonoBehaviour` in runtime core; no `UnityEvent` (C# `Action<T>` only); no
graphcore modification; no raw context-key literals at call sites (Principle VI); `com.unity.localization`
only behind the abstraction in an isolated optional adapter (Principle V + constitution v1.2.0
Dependencies rule); EditMode tests only.

**Scale/Scope**: 1 graph type, 1 line node subclass (+4 reused built-in node types), 1 choice subclass,
1 context (+keys), 1 speaker type, ~6 conditions, ~5 actions, 1 localization interface + 2 providers +
1 settings, 1 player facade + step types + 1 line executor, 5 node views, 1 edge view, 1 graph view,
1 inspector, 1 window, 1 sample builder.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.* Constitution v1.2.0.

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | Zero graphcore changes; builds only on graphcore's public API (verified against v0.2.0 source). |
| II – Universal Abstractions Only | ✅ Pass | All dialogue meaning (speaker, line, localized text, choice label) lives in this lib; graphcore stays text-agnostic. |
| III – Specification-First | ✅ Pass | `spec.md` written and validated (checklist all-pass) before this plan. |
| IV – Test-Driven Development | ✅ Pass | EditMode tests written before implementation per story; Red-Green-Refactor. |
| V – Simplicity (YAGNI) | ✅ Pass | Mirrors proven starterGraph; reuses graphcore types; **inline-only** conditions/effects (no new node patterns); CSV provider is minimal. |
| VI – Typed Context Contract | ✅ Pass | `DialogueContext : BaseContext` + `DialogueContextKeys` + `CreateCloneInstance()`; conditions/actions stay generic (key as serialized data); no key literals at call sites. |
| VII – Cross-lib via SubGraph | ✅ Pass | Reuses graphcore `SubGraphNodeData` (`BaseGraph` target); edit-time cycle refusal via `CycleDetector`, runtime via `GraphCycleException`. No sibling-lib dependency. |

**Dependencies rule (v1.2.0)**: `com.unity.localization` appears **only** in the optional adapter
assembly behind `ILocalizationProvider`; the default CSV provider works without it, and runtime core
takes no dependency on it. ✅ Compliant.

**Result**: No violations. Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/010-graphdialoguesystem-mvp/
├── plan.md          # This file
├── research.md      # Phase 0 — decisions
├── data-model.md    # Phase 1 — entities
├── quickstart.md    # Phase 1 — manual validation walkthrough
├── contracts/       # Phase 1 — public API surface (markdown)
│   └── public-api.md
└── tasks.md         # Phase 2 — /speckit-tasks (not created here)
```

### Source Code (new package, mirrors starterGraph)

```text
com.faolline.graphdialoguesystem/
├── package.json
├── Runtime/
│   ├── com.faolline.graphdialoguesystem.Runtime.asmdef   (refs graphcore.Runtime; NO external deps)
│   ├── DialogueGraph.cs                                   (BaseGraph + [CreateAssetMenu])
│   ├── DialogueContext.cs                                 (BaseContext + typed bool/int/float/string)
│   ├── DialogueContextKeys.cs                             (key consts)
│   ├── Nodes/DialogueLineNodeData.cs                      (StatementNodeData + SpeakerKey + TextKey + ExpressionKey)
│   ├── Choices/DialogueChoice.cs                          (BaseChoice + DisplayTextKey)
│   ├── Speakers/Speaker.cs                                (ScriptableObject: DisplayNameKey + expressions)
│   ├── Speakers/SpeakerExpression.cs                      (key → presentation asset)
│   ├── Conditions/  (ComparisonOperator + bool/int/float/string + always-true/false)
│   ├── Actions/     (log + set bool/int/float/string)
│   ├── Execution/
│   │   ├── DialogueLineExecutor.cs                        (INodeExecutor for the line node type)
│   │   └── DialogueExecutorRegistryFactory.cs            (builds a configured NodeExecutorRegistry)
│   ├── Playback/
│   │   ├── DialoguePlayer.cs                              (wraps BaseRunner; emits localized steps)
│   │   ├── DialogueStep.cs / LineStep / ChoiceStep / EndStep
│   │   └── ChoiceOption.cs                                (presented option: id, label, available)
│   └── Localization/
│       ├── ILocalizationProvider.cs                       (neutral text-resolution contract)
│       ├── CsvLocalizationProvider.cs                     (default, dependency-free)
│       ├── LocalizationSettings.cs                        (active provider + locale; safe default)
│       └── LocalizationContext.cs                         (ambient/current provider accessor)
├── Localization.Unity/                                    (OPTIONAL adapter — isolated)
│   ├── com.faolline.graphdialoguesystem.Localization.Unity.asmdef
│   │        (refs Runtime + com.unity.localization; versionDefines + defineConstraints gate it)
│   └── UnityLocalizationProvider.cs                       (ILocalizationProvider over String Tables)
├── Editor/
│   ├── com.faolline.graphdialoguesystem.Editor.asmdef    (refs graphcore.Runtime+Editor, lib Runtime)
│   ├── Edges/DialogueEdgeView.cs
│   ├── Nodes/StartNodeView, EndNodeView, DialogueLineNodeView, ChoiceNodeView, SubGraphNodeView
│   ├── Graph/DialogueGraphView.cs                         (CreateNodeView dispatch + context menu)
│   ├── Inspector/DialogueNodeInspectorView.cs            (line speaker/text, choices, EndReason, subgraph, params, base-node)
│   ├── Window/DialogueGraphEditorWindow.cs               (Run/Choose/Continue/GoBack/Checkpoint; multi-window)
│   └── Samples/DialogueSampleBuilder.cs                   (menu: generate sample dialogue + sub-dialogue)
└── Tests/EditMode/
    ├── com.faolline.graphdialoguesystem.Tests.EditMode.asmdef
    ├── Runtime/  (context contract, conditions, actions, graph, line node, player, CSV provider, speaker)
    └── Editor/   (node views, graph view, inspector, window/execution, reload/reconnect)
```

**Structure Decision**: New package `com.faolline.graphdialoguesystem` with four assemblies (Runtime,
optional Localization.Unity adapter, Editor, Tests.EditMode), mirroring the validated starterGraph
layout plus one isolated adapter assembly for the Unity Localization integration. Editor views extend
graphcore's `BaseGraphView`/`BaseNodeView`/`BaseEdgeView`/`BaseNodeInspectorView`/`BaseGraphEditorWindow`,
inheriting LoadGraph data-safety, edge reconnection, multi-window, and cycle utilities — only the
dialogue-specific dispatch/sections/window/executor/localization are written here.

## Phase 0: Research

### R-001 — Derive from the validated starterGraph reference

- **Decision**: Implement each editor/runtime type by mirroring its proven starterGraph counterpart
  (renamed to the dialogue domain), then add the dialogue specifics (speaker, localized text, player
  facade, localization providers).
- **Rationale**: Simplicity (V) and risk reduction — starterGraph already passes the full EditMode
  suite for canvas dispatch, dynamic choice ports routed by id, inspector sections, the pause/resume
  run loop, typed conditions/actions, sub-graph cycle refusal, and reload/reconnect.
- **Alternatives considered**: Designing fresh or porting the old `com.faolline.dialoguesystem~` editor
  (rejected: the old custom graph view/runner/persistence are exactly what graphcore replaces).

### R-002 — Domain node modeling: subclass only where text is added

- **Decision**: Reuse `StartNodeData`, `ChoiceNodeData`, `EndNodeData`, `SubGraphNodeData` unchanged.
  Add only `DialogueLineNodeData : StatementNodeData` with `SpeakerKey`, `TextKey`, `ExpressionKey`
  (its own `NodeTypeId = "graphdialogue/line"`). `DialogueChoice : BaseChoice` adds `DisplayTextKey`.
- **Rationale**: graphcore's built-ins already carry identity, position, color, `IsCheckpoint`,
  `EntryConditions`, `OnEnterActions`, `OnExitActions` — the dialogue layer only needs to attach
  speaker + localized text. Abandons the old `SentenceType` enum (II, V).
- **Alternatives considered**: A single generic node carrying a type enum (rejected: not idiomatic to
  graphcore; loses per-type views/executors).

### R-003 — Reactivity is inline-only

- **Decision**: No condition/effect node types. Conditions are `BaseCondition` subclasses attached to
  `DialogueChoice.Condition`, `BaseEdgeData.Condition`, or `BaseNodeData.EntryConditions`; effects are
  `BaseAction` subclasses on `OnEnterActions`/`OnExitActions`. Ship a minimal generic set
  (bool/int/float/string conditions with a `ComparisonOperator`; set-bool/int/float/string + log
  actions), keyed by serialized string (no call-site literals).
- **Rationale**: This is graphcore's native model; condition/effect nodes are a pattern absent from
  graphcore and starterGraph and would add scope for no MVP value (V). The runner already evaluates
  entry conditions, enter/exit actions, and edge/choice conditions.
- **Alternatives considered**: Dedicated visual condition/effect nodes (rejected for this iteration:
  YAGNI; explicitly out of scope in the spec).

### R-004 — Localization: lib-owned abstraction + 2 providers, adapter isolated

- **Decision**: Define `ILocalizationProvider` in the runtime core (resolves a key + active locale →
  string, with a defined fallback). Ship `CsvLocalizationProvider` (default, dependency-free) and an
  **optional** `UnityLocalizationProvider` in a **separate assembly**
  (`…Localization.Unity.asmdef`) that references `com.unity.localization`, gated by `versionDefines` +
  `defineConstraints` so it compiles only when the package is present. A lightweight
  `LocalizationSettings` selects the active provider + locale; a safe default is used when unconfigured.
- **Rationale**: Directly satisfies the chosen "abstraction + 2 providers" and constitution v1.2.0:
  Unity Localization never touches runtime core; projects without it incur no dependency. The previous
  lib's hard `com.unity.localization` coupling is removed.
- **Alternatives considered**: CSV-only (rejected: drops the engine-localization path the project
  wants) / Unity-Localization-only (rejected: hard external dependency, fails out-of-the-box usage).

### R-005 — Playback facade emitting localized steps

- **Decision**: Provide `DialoguePlayer` wrapping a `BaseRunner`. It subscribes to runner events and
  re-emits domain steps: `LineStep` (resolved speaker display name + resolved line text + expression),
  `ChoiceStep` (each option's resolved label + availability), `EndStep` (end reason). A
  `DialogueLineExecutor : INodeExecutor` handles `DialogueLineNodeData` entry (exposes speaker/text to
  the step). The player resolves text through the injected `ILocalizationProvider`.
- **Rationale**: Keeps `BaseRunner` reuse intact (no custom runner) while giving the game UI the
  line/choice/end shape it expects (improving on the old `DialogueStep` union). Availability uses the
  same condition evaluation the runner uses for choices.
- **Alternatives considered**: Expose `BaseRunner` directly (rejected: the game would re-implement
  localization + speaker resolution + choice availability each time).

### R-006 — Speaker as an improved ScriptableObject

- **Decision**: Port `Speaker` as a `ScriptableObject` with a localizable `DisplayNameKey` (resolved
  through `ILocalizationProvider`) and a list of `SpeakerExpression` (key → presentation asset) with a
  fallback. No avatar spawning/animation (out of scope; the player reports keys/assets, UI presents).
- **Rationale**: Preserves the useful old concept while routing the name through the new localization
  abstraction; presentation stays the game's concern (II, scope).
- **Alternatives considered**: Inline speaker fields on the node (rejected: speakers are shared across
  many lines; an asset is the right granularity, matching the old design).

### R-007 — Headless verification strategy

- **Decision**: Every behavior is covered by EditMode tests built around in-memory `DialogueGraph` +
  `DialoguePlayer` + a stub/CSV `ILocalizationProvider`; editor tests drive the graph view/inspector/
  window as starterGraph's tests do. No PlayMode tests.
- **Rationale**: Constitution IV + the project memory ("maximize headless testing"); `BaseRunner` and
  the player are headless by design.

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](data-model.md).

### Contracts

See [contracts/public-api.md](contracts/public-api.md) — the public runtime surface a game/editor
consumer depends on (`DialoguePlayer`, step types, `ILocalizationProvider`, providers, settings,
`DialogueContext`/keys, node/choice/speaker types).

### Key Design Decisions

**D-001 — Runtime socle (US1/US2)**: `DialogueGraph`, `DialogueContext`/`Keys`,
`DialogueLineNodeData`, `DialogueChoice`, `Speaker`/`SpeakerExpression`, conditions/actions,
`DialogueLineExecutor` + registry factory. Conditions/actions are `ScriptableObject`s with serialized
keys.

**D-002 — Localization (US4)**: `ILocalizationProvider` + `CsvLocalizationProvider` (default) in core;
`UnityLocalizationProvider` in the isolated optional adapter; `LocalizationSettings` selects provider +
locale with a safe default and defined fallback on missing keys.

**D-003 — Playback (US2/US3)**: `DialoguePlayer` over `BaseRunner` emitting `LineStep`/`ChoiceStep`/
`EndStep`; choice availability via condition evaluation; sub-dialogue + cycle handling inherited;
step-back via `GoBack`/`GoBackToCheckpoint`.

**D-004 — Editor (US1)**: `DialogueGraphView` (`CreateNodeView` dispatch + context menu), five node
views, `DialogueEdgeView`, `DialogueNodeInspectorView` (line speaker/text fields, choice
add/remove/label/condition with live ports + edge reconnect, EndReason, sub-graph target with cycle
refusal, typed parameter panel, shared base-node section), `DialogueGraphEditorWindow` (Run/Choose/
Continue/GoBack/Checkpoint, per-asset multi-window).

**D-005 — Robustness (US1)**: Inherited from `BaseGraphView` (LoadGraph data-safety, reconnect) and the
window's `OnOpenAsset`; cycle refusal via `CycleDetector`; sample generator menu.

### Agent Context

`CLAUDE.md` is updated to point at `specs/010-graphdialoguesystem-mvp/plan.md`.

## Complexity Tracking

No constitution violations. No entries required.
