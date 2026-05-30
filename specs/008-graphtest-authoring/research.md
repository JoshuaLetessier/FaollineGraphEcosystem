# Research: GraphTest — Editor Authoring Gaps

**Feature**: `008-graphtest-authoring` | **Date**: 2026-05-30

All three user stories sit on top of existing graphcore runtime support; research focused on
confirming that support and choosing the thinnest editor-side wiring. No `NEEDS CLARIFICATION`
remained from the spec.

## R-001 — EndReason editing (US1)

- **Decision**: Inspector renders a UI Toolkit `EnumField` for `EndNodeData.EndReason`, mutating the node directly and marking the graph dirty. Persistence is automatic via the existing `[SerializeField]`.
- **Rationale**: Matches the established direct-mutation inspector pattern (choice section); the runtime already logs the reason.
- **Alternatives considered**: `SerializedProperty`/`PropertyField` binding — rejected as heavier for a single enum.

## R-002 — Edit-time SubGraph cycle refusal (US2)

- **Decision**: On `TargetGraph` change in the inspector, call `CycleDetector.Check(currentGraph, proposed)`; revert + log on cycle.
- **Rationale**: Inspector assignment bypasses `HandleEdgeCreation`'s edge-time check, so the guard must live at the assignment site. `CycleDetector` already implements the DFS.
- **Alternatives considered**: Runtime-only detection (rejected: FR-010 needs edit-time refusal); graphcore-side check on `TargetGraph` setter (rejected: graphcore change).

## R-003 — SubGraph execution in the window loop (US2)

- **Decision**: No special `DrainLoop` handling; `BaseRunner` enters/exits sub-graphs inside `Proceed()`. Null target → existing `OnStuck`; nested Choice still pauses.
- **Rationale**: The runner owns traversal; the editor loop stays a thin driver. Satisfies FR-009/FR-011 with no new logic.
- **Alternatives considered**: Detecting sub-graph entry in the window to log a banner — rejected (unnecessary; per-node logs already show the descent).

## R-004 — Typed condition comparison model (US3)

- **Decision**: `TestIntCondition`/`TestFloatCondition` = key + `ComparisonOperator` enum (Equal/NotEqual/Less/LessOrEqual/Greater/GreaterOrEqual) + expected value. `TestStringCondition` = key + expected + negate. All null-safe on missing/mistyped keys.
- **Rationale**: Operator enum covers ordered comparisons with one concrete type per value kind (good Unity inspector ergonomics); strings need only (in)equality. No expression language (YAGNI).
- **Alternatives considered**: A single generic comparison type — rejected (`[SerializeReference]` + inspector clarity favor concrete per-type classes, like `TestBoolCondition`).

## R-005 — Parameter panel generalization (US3)

- **Decision**: Panel lists all parameters and adds via (key, `ParameterType` enum, default string). `AddBoolParameter`/`RemoveBoolParameter` retained as wrappers for backward compatibility.
- **Rationale**: `ParameterData` already carries `Type` + string `DefaultValue`, and `BaseContext.InitFromGraph` already parses Bool/Int/Float/String. The only gap is the panel — a purely presentational change.
- **Alternatives considered**: Per-type default widgets — deferred; a string default parsed per type is sufficient and simplest.

## R-006 — Generic conditions/actions vs. typed context (Principle VI)

- **Decision**: Conditions/actions stay generic (`BaseContext` + serialized `ParameterKey` data). No mandatory `TestGameContext` change; optional sample typed properties follow the keys-class + `CreateCloneInstance()` pattern.
- **Rationale**: Principle VI targets C# call-site key literals; a configurable condition/action key is data, not a literal — exactly how `TestBoolCondition` already works.
- **Alternatives considered**: Typed-context-per-test-param — rejected as scope creep for a verification package.

## Confirmed graphcore support (no change required)

- `EndNodeData.EndReason` (`EndReason` enum: Completed/Cancelled/Error) — serialized.
- `SubGraphNodeData` (`TargetGraph: BaseGraph`, `InheritParentContext: bool`); `BaseRunner.EnterSubGraph`/`HandleEndNode` handle descent/return and throw `GraphCycleException` on recursion.
- `ParameterType` enum (Bool/Int/Float/String); `BaseContext.Set<T>/TryGet<T>` supports all four; `InitFromGraph` parses all four with invariant culture and warns on parse failure.
- `CycleDetector.Check(root, proposed)` (graphcore Editor) — reusable for edit-time refusal.
