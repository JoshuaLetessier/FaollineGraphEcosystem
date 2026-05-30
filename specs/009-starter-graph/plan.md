# Implementation Plan: starterGraph — Reusable Downstream-Lib Starter

**Branch**: `009-starter-graph` | **Date**: 2026-05-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/009-starter-graph/spec.md`

## Summary

Create `com.faolline.starterGraph`: a clean, reusable starting point for any downstream ecosystem
lib, built only on `com.faolline.graphcore`. It is the generalized twin of the validated
`com.faolline.graphTest` package — it ships the full runtime, editor, and interface plumbing
(every node type incl. **SubGraph**, the complete inspector, the execution/navigation window,
the typed-context contract, typed conditions/actions, the typed parameter panel, edge-reconnect-
on-reload, no-data-loss load, and multi-window) so a developer copies/renames it and only adds
their domain. **No graphcore change** — graphTest already proved every capability, and the editor
robustness (LoadGraph data-safety + edge reconnection) lives in graphcore's `BaseGraphView`, which
the starter inherits for free.

## Technical Context

**Language/Version**: C# 9 (Unity 6000.3.x)

**Primary Dependencies**: `com.faolline.graphcore` (runtime + editor)

**Storage**: Unity `ScriptableObject` assets; choices via `[SerializeReference]`; conditions/actions are `BaseCondition`/`BaseAction` `ScriptableObject`s

**Testing**: NUnit via Unity Test Runner (EditMode only)

**Target Platform**: Unity Editor (+ headless runtime)

**Project Type**: New downstream package with separate Runtime/Editor assembly definitions

**Performance Goals**: Inspector edits reflect in under one frame; Run/Choose under 1 second; graph load linear in nodes+edges

**Constraints**: No `MonoBehaviour`; no `UnityEvent` (C# `Action<T>` only); EditMode tests only; **no graphcore modification**; no raw key literals at call sites (Principle VI)

**Scale/Scope**: ~1 graph type, 1 context (+keys), 1 choice, 6 conditions, 5 actions, 1 example node, 5 node views, 1 edge view, 1 inspector (6 sections), 1 window, 1 sample generator

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | Zero graphcore changes. Builds only on existing public graphcore API. |
| II – Universal Abstractions Only | ✅ Pass | All domain-ish types (Starter*) live in the starter package, never in graphcore. |
| III – Specification-First | ✅ Pass | `spec.md` written, validated (checklist all-pass). |
| IV – Test-Driven Development | ✅ Pass | Red-Green-Refactor per story; EditMode tests before implementation. |
| V – Simplicity (YAGNI) | ✅ Pass | Mirrors the already-proven graphTest structure; no new patterns invented; typed conditions use a minimal operator/equality model. |
| VI – Typed Context Contract | ✅ Pass | `StarterContext : BaseContext` + `StarterContextKeys` + `CreateCloneInstance()`; typed bool/int/float/string properties; conditions/actions stay generic (key = serialized data). |
| VII – Cross-lib via SubGraph | ✅ Pass | SubGraph node exposes `TargetGraph` as `BaseGraph`; cycle detection at edit time (CycleDetector) and runtime (`GraphCycleException`). |

**Result**: No violations. Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/009-starter-graph/
├── plan.md          # This file
├── research.md      # Phase 0 — decisions
├── data-model.md    # Phase 1 — entities
├── quickstart.md    # Phase 1 — manual validation walkthrough
└── tasks.md         # Phase 2 — /speckit-tasks (not created here)
```

No `contracts/` directory: the starter is internal Unity tooling/library with no external consumer
API surface beyond its public types (captured in data-model.md), consistent with features 005–008.

### Source Code (new package, mirrors graphTest)

```text
com.faolline.starterGraph/
├── package.json
├── Runtime/
│   ├── com.faolline.starterGraph.Runtime.asmdef        (refs graphcore.Runtime)
│   ├── StarterGraph.cs                                 (BaseGraph + [CreateAssetMenu])
│   ├── StarterContext.cs                               (BaseContext + typed bool/int/float/string)
│   ├── StarterContextKeys.cs                           (key consts)
│   ├── Choices/StarterChoice.cs                        (BaseChoice + Label)
│   ├── Nodes/StarterStatementNodeData.cs               (StatementNodeData + Label)
│   ├── Conditions/
│   │   ├── ComparisonOperator.cs
│   │   ├── StarterAlwaysTrueCondition.cs / StarterAlwaysFalseCondition.cs
│   │   ├── StarterBoolCondition.cs
│   │   └── StarterIntCondition.cs / StarterFloatCondition.cs / StarterStringCondition.cs
│   └── Actions/
│       ├── StarterLogAction.cs
│       └── StarterSetBoolAction.cs / Int / Float / String
├── Editor/
│   ├── com.faolline.starterGraph.Editor.asmdef         (refs graphcore.Runtime+Editor, starter.Runtime)
│   ├── Edges/StarterEdgeView.cs
│   ├── Nodes/StartNodeView, EndNodeView, StarterStatementNodeView, ChoiceNodeView, SubGraphNodeView
│   ├── Graph/StarterGraphView.cs                       (CreateNodeView dispatch + context menu)
│   ├── Inspector/StarterNodeInspectorView.cs           (label, EndReason, choice, subgraph, param panel, base-node)
│   ├── Window/StarterGraphEditorWindow.cs              (Run/Choose/Continue/GoBack/Checkpoint + pause; multi-window)
│   └── Samples/StarterSampleBuilder.cs                 (menu: generate sample graph)
└── Tests/EditMode/
    ├── com.faolline.starterGraph.Tests.EditMode.asmdef
    ├── Runtime/  (context contract, conditions, actions, graph)
    └── Editor/   (node views, graph view, inspector, window/execution, reload/reconnect)
```

**Structure Decision**: New package `com.faolline.starterGraph` with three assemblies (Runtime,
Editor, Tests.EditMode), mirroring the validated `com.faolline.graphTest` layout. Editor views
extend graphcore's `BaseGraphView`/`BaseNodeView`/`BaseEdgeView`, so the LoadGraph data-safety fix,
edge reconnection, and cycle utilities are inherited — only the starter-specific dispatch/sections/
window are written here.

## Phase 0: Research

### R-001 — Derive from the validated graphTest reference

- **Decision**: Implement each Starter* type by mirroring its proven graphTest counterpart (renamed, generalized), rather than designing from scratch. graphTest is the working reference that already passes the full EditMode suite covering exactly these behaviors.
- **Rationale**: Simplicity (Principle V) and risk reduction — the patterns (dynamic choice ports routed by id, inspector sections, pause/resume loop, typed conditions/actions) are validated. The starter is the clean public version.
- **Alternatives considered**: Designing fresh (rejected: needless risk and divergence from proven behavior).

### R-002 — Typed context covering all four types (Principle VI)

- **Decision**: `StarterContext` exposes example typed properties for **bool, int, float, and string** (graphTest's context only modelled bools), each going through `StarterContextKeys`, with `CreateCloneInstance()` overridden. This fully demonstrates the contract a downstream lib copies.
- **Rationale**: The starter must show the complete pattern for every supported type; the de-risk tests proved the runner restores all four across GoBack.
- **Alternatives considered**: Bool-only (rejected: incomplete model for a starter).

### R-003 — Editor robustness inherited from graphcore

- **Decision**: Reuse graphcore's `BaseGraphView` as-is for LoadGraph (no data loss), `ReconnectNodeEdges`, and edge reconnection on reload; reuse `CycleDetector` for edit-time refusal. The starter adds only the multi-window `OnOpenAsset` behavior (focus-or-create per asset) in its window.
- **Rationale**: Fixed/validated in graphcore during 007/008; extending `BaseGraphView` inherits them. No duplication, no graphcore change.
- **Alternatives considered**: Re-implement in the starter (rejected: duplication).

### R-004 — Full condition/action set with a minimal comparison model

- **Decision**: Conditions = always-true, always-false, bool, int (operator), float (operator), string (equality+negate); actions = log, set bool/int/float/string. Numeric conditions use a `ComparisonOperator` enum; all conditions are null-safe (false + warning on missing/mistyped key).
- **Rationale**: Matches the proven graphTest set; covers all four parameter types; YAGNI (no expression language).

### R-005 — Self-contained sample generator

- **Decision**: An editor menu builds a sample `StarterGraph` (and a child graph for the SubGraph) exercising choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters — conditions/actions stored as sub-assets so the sample is portable.
- **Rationale**: Mirrors graphTest's sample builder; gives the developer an immediate runnable demonstration of the whole starter.

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](data-model.md).

### Key Design Decisions

**D-001 — Runtime socle (US1)**: `StarterGraph`, `StarterContext`/`Keys` (bool/int/float/string typed properties + `CreateCloneInstance`), `StarterChoice`, six conditions, five actions, `StarterStatementNodeData`. Conditions/actions are `ScriptableObject`s with serialized keys (no call-site literals).

**D-002 — Node views & graph view (US2)**: Five node views (Start, End, Statement, Choice with dynamic id-routed output ports, SubGraph 1-in/1-out) + `StarterEdgeView`; `StarterGraphView` dispatches in `CreateNodeView` and adds each type to the context menu.

**D-003 — Inspector (US2)**: `StarterNodeInspectorView` with sections: label (Statement), EndReason enum (End), choice add/remove/label/condition with live ports + edge reconnect, sub-graph target+inherit with cycle refusal, typed parameter panel (bool/int/float/string), and the shared base-node section.

**D-004 — Window (US2)**: `StarterGraphEditorWindow` with Run, Choose (condition-passing choices), Continue, GoBack, GoBackToCheckpoint; pause-at-choice drain loop; per-asset multi-window via `OnOpenAsset`.

**D-005 — Robustness (US3)**: Inherited from `BaseGraphView` (LoadGraph data-safety, reconnect); multi-window in the window; cycle refusal via `CycleDetector`; sample generator menu.

### Agent Context

`CLAUDE.md` is updated to point at `specs/009-starter-graph/plan.md`.

## Complexity Tracking

No constitution violations. No entries required.
