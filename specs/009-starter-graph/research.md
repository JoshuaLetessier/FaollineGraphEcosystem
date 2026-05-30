# Research: starterGraph — Reusable Downstream-Lib Starter

**Feature**: `009-starter-graph` | **Date**: 2026-05-30

The starter generalizes the already-validated `graphTest` package; research focused on confirming
the reference and the reuse boundaries. No `NEEDS CLARIFICATION` remained from the spec.

## R-001 — Derive from the validated graphTest reference

- **Decision**: Mirror each graphTest type as a `Starter*` equivalent (renamed, generalized) rather than designing fresh.
- **Rationale**: graphTest already passes the full EditMode suite for exactly these behaviors; reuse maximizes simplicity (Principle V) and minimizes risk.
- **Alternatives considered**: From-scratch design (rejected: needless divergence/risk).

## R-002 — Typed context covering bool/int/float/string (Principle VI)

- **Decision**: `StarterContext` exposes typed example properties for all four supported types via `StarterContextKeys`, with `CreateCloneInstance()` overridden.
- **Rationale**: The starter must demonstrate the complete typed-context contract; the de-risk tests proved the runner restores all four types across GoBack.
- **Alternatives considered**: Bool-only (rejected: incomplete model).

## R-003 — Editor robustness inherited from graphcore

- **Decision**: Extend `BaseGraphView`/`BaseNodeView`/`BaseEdgeView` so the LoadGraph data-safety, `ReconnectNodeEdges`, and edge reconnection on reload come for free; reuse `CycleDetector` for edit-time cycle refusal. Add only multi-window `OnOpenAsset` in the starter window.
- **Rationale**: Those were fixed/validated in graphcore (007/008); inheriting avoids duplication and any graphcore change.
- **Alternatives considered**: Re-implement per package (rejected: duplication).

## R-004 — Condition/action set + minimal comparison model

- **Decision**: Conditions = always-true/false, bool, int (operator), float (operator), string (equality+negate); actions = log, set bool/int/float/string. `ComparisonOperator` enum for numerics; all conditions null-safe (false+warning on missing/mistyped key).
- **Rationale**: Matches the proven graphTest set; covers all four types; YAGNI.
- **Alternatives considered**: Single generic comparison type / expression language (rejected: inspector ergonomics / scope).

## R-005 — Self-contained sample generator

- **Decision**: An editor menu builds a parent `StarterGraph` + a child graph exercising choices, conditions, actions, a checkpoint, a sub-graph, and typed params; conditions/actions stored as sub-assets.
- **Rationale**: Immediate runnable demonstration; mirrors graphTest's sample builder.
- **Alternatives considered**: No sample (rejected: a starter should ship a working example).

## Confirmed reuse boundaries (no graphcore change)

- graphcore provides: `BaseGraph`, `BaseContext` (bool/int/float/string + `InitFromGraph` parsing + DeepClone/CopyValuesFrom), `BaseRunner` (history, checkpoints, sub-graph entry/exit, `GraphCycleException`), `BaseChoice`, `ChoiceNodeData`, `SubGraphNodeData`, `EndNodeData.EndReason`, `ParameterData`/`ParameterType`, `BaseGraphView` (LoadGraph data-safe + ReconnectNodeEdges + edge reconnect), `BaseNodeView`, `BaseEdgeView`, `BaseNodeInspectorView`, `BaseGraphEditorWindow`, `CycleDetector`.
- The starter writes only `Starter*` types + the editor dispatch/sections/window/sample.
