# Phase 0 Research: graphdialoguesystem (MVP)

**Feature**: `010-graphdialoguesystem-mvp` | **Date**: 2026-05-31

All NEEDS CLARIFICATION from the Technical Context are resolved here. Each decision is grounded in the
**actual** graphcore v0.2.0 source and the validated starterGraph package (both read during planning).

## R-001 — Derive from the validated starterGraph reference

- **Decision**: Mirror each starterGraph type into the dialogue domain, then add dialogue specifics.
- **Rationale**: starterGraph passes the full EditMode suite for exactly the mechanics needed (canvas
  `CreateNodeView` dispatch, dynamic choice ports routed by `portName = choice.Id`, inspector sections
  with live port rebuild + `ReconnectNodeEdges`, the Run/Choose drain loop, sub-graph cycle refusal via
  `CycleDetector`, reload data-safety inherited from `BaseGraphView`).
- **Alternatives considered**: Port the old `com.faolline.dialoguesystem~` editor/runner (rejected: its
  `CustomGraphView`, `DialogueRunner`, `GraphSaveUtility`, `DialogueGraphModel` are precisely what
  graphcore replaces); design fresh (rejected: needless risk).

## R-002 — Domain node modeling: subclass only where text is added

- **Decision**: Reuse `StartNodeData` (`"graphcore/start"`), `ChoiceNodeData` (`"graphcore/choice"`),
  `EndNodeData` (`"graphcore/end"`, carries `EndReason`), `SubGraphNodeData` (`"graphcore/subgraph"`)
  unchanged. Add `DialogueLineNodeData : StatementNodeData` with `NodeTypeId = "graphdialogue/line"`,
  fields `SpeakerKey` (string), `TextKey` (string, localized), `ExpressionKey` (string, default
  `"neutral"`). `DialogueChoice : BaseChoice` adds `DisplayTextKey` (string, localized).
- **Rationale**: `BaseNodeData` already provides `Id`, `NodeType`, `Position`, color override,
  `IsCheckpoint`, `EntryConditions`, `OnEnterActions`, `OnExitActions`; `BaseChoice` already provides
  `Id` + `Condition`. The dialogue layer adds only the text/speaker fields. Abandons the old
  `SentenceType` enum (constitution II/V).
- **Alternatives considered**: Generic node + type enum (rejected: breaks per-type views/executors,
  non-idiomatic).

## R-003 — Reactivity is inline-only (no condition/effect nodes)

- **Decision**: Conditions = `BaseCondition` subclasses on `DialogueChoice.Condition`,
  `BaseEdgeData.Condition`, `BaseNodeData.EntryConditions`. Effects = `BaseAction` subclasses on
  `OnEnterActions`/`OnExitActions`. Ship a minimal generic set keyed by serialized string:
  - Conditions: `AlwaysTrue`, `AlwaysFalse`, `BoolCondition`, `IntCondition`, `FloatCondition`,
    `StringCondition` (numeric ones use a `ComparisonOperator { Equal, NotEqual, Less, LessOrEqual,
    Greater, GreaterOrEqual }`; all null-safe → false + `[GraphDialogue]` warning on missing/mistyped).
  - Actions: `LogAction`, `SetBoolAction`, `SetIntAction`, `SetFloatAction`, `SetStringAction`.
- **Rationale**: This is graphcore's native model and the starterGraph/graphTest proven set; condition/
  effect nodes are absent everywhere and out of scope (V). The runner already runs entry conditions,
  enter/exit actions, and edge/choice conditions.
- **Alternatives considered**: Visual condition/effect nodes (rejected: YAGNI, explicit out-of-scope).

## R-004 — Localization: lib-owned abstraction + 2 providers, adapter isolated

- **Decision**:
  - `ILocalizationProvider` (runtime core): `string Resolve(string key, string locale)` plus a way to
    query the active locale; returns a defined fallback (e.g. `$"#{key}"`) + `[GraphDialogue]` warning
    when a key is absent, never empty/broken output.
  - `CsvLocalizationProvider` (runtime core, **no external dependency**): loads a simple CSV table
    (`Key,locale1,locale2,…`) into a `key → (locale → text)` map; the default provider.
  - `UnityLocalizationProvider` (**separate optional assembly** `…Localization.Unity`): implements
    `ILocalizationProvider` over `com.unity.localization` String Tables. The asmdef references
    `com.unity.localization` and is gated with `versionDefines`/`defineConstraints` so it only compiles
    when the package is installed. Runtime core never references it.
  - `LocalizationSettings`: lightweight selection of active provider + current locale; if none
    configured, a safe default provider is used so playback never fails for lack of setup.
- **Rationale**: Implements the chosen "abstraction + 2 providers" and constitution v1.2.0 Dependencies
  rule (Unity Localization only behind the abstraction, in an optional adapter, with a non-Unity
  default provider). Removes the old lib's hard coupling to `com.unity.localization`.
- **Alternatives considered**: CSV-only (rejected: loses engine-localization path); Unity-only
  (rejected: hard dependency, no out-of-the-box use); reflection-based provider switch (rejected:
  asmdef gating is simpler and compile-safe).

## R-005 — Playback facade emitting localized steps

- **Decision**: `DialoguePlayer` wraps a `BaseRunner` (composition, not subclass). It owns a
  `NodeExecutorRegistry` built by a factory (registering `DialogueLineExecutor`), a `DialogueContext`,
  and an `ILocalizationProvider`. It subscribes to runner events and re-emits:
  - `LineStep` { NodeId, SpeakerId, ResolvedSpeakerName, ResolvedText, ExpressionKey }
  - `ChoiceStep` { NodeId, IReadOnlyList<ChoiceOption> } where `ChoiceOption` { ChoiceId,
    ResolvedLabel, Available }
  - `EndStep` { NodeId, EndReason }
  exposed as `event Action<LineStep> OnLine`, `Action<ChoiceStep> OnChoices`, `Action<EndStep> OnEnded`.
  Drives `Proceed()`, `ChooseById()`, `GoBack()`, `GoBackToCheckpoint()`. Choice availability is the
  same condition evaluation the runner/starter window uses.
- **Rationale**: Reuses `BaseRunner` wholesale (no custom runner) while giving the UI the
  line/choice/end shape (an improvement over the old read-only `DialogueStep` union). Sub-dialogue
  nesting, history, and cycle handling come for free from `BaseRunner`.
- **Alternatives considered**: Expose `BaseRunner` raw (rejected: pushes localization + speaker
  resolution + availability into every consumer).

## R-006 — Speaker as an improved ScriptableObject

- **Decision**: `Speaker : ScriptableObject` with `SpeakerId` (logical, not translated),
  `DisplayNameKey` (resolved via `ILocalizationProvider`), `DisplayNameFallback` (literal), and
  `List<SpeakerExpression>` (`Key` → presentation `UnityEngine.Object`/prefab) + a fallback expression.
  `TryGetExpression(key, out asset)` falls back safely. No avatar spawning/animation.
- **Rationale**: Keeps the useful old concept; routes the name through the new abstraction; presentation
  stays the game's concern (II + scope).
- **Alternatives considered**: Inline speaker fields per node (rejected: speakers are shared; asset
  granularity matches reuse and the old design).

## R-007 — Headless verification strategy

- **Decision**: EditMode tests only. Runtime tests build in-memory `DialogueGraph` + `DialoguePlayer`
  with a CSV/stub `ILocalizationProvider`; editor tests drive `DialogueGraphView`/inspector/window like
  starterGraph's tests. Each behavior gets a failing test first (IV).
- **Rationale**: Constitution IV + project memory "maximize headless testing"; everything needed is
  headless (`BaseRunner`, player, providers).

## Resolved unknowns summary

| Unknown | Resolution |
|---------|-----------|
| Node model | Reuse 4 built-ins; add `DialogueLineNodeData` + `DialogueChoice` only (R-002) |
| Conditions/effects | Inline-only, minimal generic set (R-003) |
| Localization | `ILocalizationProvider` + CSV default + isolated optional Unity adapter (R-004) |
| Playback shape | `DialoguePlayer` facade over `BaseRunner` with localized steps (R-005) |
| Speakers | Improved `ScriptableObject`, name via provider, no avatar presentation (R-006) |
| Variables/blackboard | `DialogueContext : BaseContext` + keys; no custom system (R-002/plan) |
| Testing | EditMode-only, headless, test-first (R-007) |
