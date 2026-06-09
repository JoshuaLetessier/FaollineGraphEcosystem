# Implementation Plan: gameflow editor authoring (slice 2)

**Branch**: `021-gameflow-editor-authoring` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

**Input**: `specs/021-gameflow-editor-authoring/spec.md`

## Summary

Give `com.faolline.graphgameflow` an **Editor** so a gameflow graph can be created and authored entirely in
the Unity editor, mirroring `com.faolline.starterGraph` (the project's "base to duplicate"). Add a creatable
`GameFlowGraph : BaseGraph` asset, a `GameFlowGraphEditorWindow` + `GameFlowGraphView` with node views for the
universal node set (Start/Statement/Choice/SubGraph/End) + an edge view, a `GameFlowNodeInspectorView`, a
`GameFlowSampleBuilder` menu that generates the runnable reference scene-flow, and a `[CreateAssetMenu]` on the
slice-1 `LoadSceneAction`. The editor **reuses graphcore's infrastructure** (`BaseGraphView`,
`BaseGraphEditorWindow`, `BaseNodeView`, `BaseNodeInspectorView`, `BaseEdgeView`, `GraphValidator`,
copy/paste, groups) and adds only the gameflow subclasses. graphcore/graphstandard are untouched, the slice-1
runtime is unchanged (its 654 EditMode + 8 PlayMode tests stay green), and gameflow bumps **0.1.0 → 0.2.0**.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0; UI Toolkit + `UnityEditor.Experimental.GraphView`.
**Primary Dependencies**: `com.faolline.graphcore` **Runtime + Editor** (`BaseGraph`, the universal node
types, and the entire editor base: views, inspector base with `AddBaseNodeSection`, edge view, validator,
cycle detector). **Storage**: `.asset` files (the graph + sub-asset actions), via `AssetDatabase`.
**Testing**: NUnit EditMode (batchmode). **Target Platform**: Unity Editor (the Editor assembly is
Editor-only). **Project Type**: editor tooling on the host package. **Constraints**: graphcore/graphstandard
untouched; slice-1 runtime unchanged + green; `[GraphGameFlow]` prefix; one class per file; node-view styling
via USS (no inline CSS); XML docs. **Scope**: ~11 new editor files + 1 new runtime type + 1 attribute + tests
+ a new Editor asmdef + package bump.

**Key reuse finding**: graphcore's `BaseNodeInspectorView.AddBaseNodeSection` already renders PropertyFields
for `_title`, `_isCheckpoint`, `_entryConditions`, **`_onEnterActions`**, `_onExitActions` (+ color). So
attaching a `LoadSceneAction` to a node is **already supported** by the base section (drop the asset into the
`On Enter Actions` list). It does **not** render `_awaitSignal` / `_waitDuration` (added to `BaseNodeData`
after that base inspector) — so the gameflow inspector's only new field work is a small "Flow" foldout adding
those two PropertyFields. Nodes are named via the existing `_title` field; no statement-label subclass needed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore + graphstandard untouched. Slice-1 gameflow runtime unchanged; its 654 EditMode + 8 PlayMode tests stay green. Additive: new asset type + editor + an attribute. gameflow 0.1.0 → 0.2.0 (MINOR). |
| II. Universal Abstractions Only | ✅ PASS | The authored node set is the UNIVERSAL set (Start/Statement/Choice/SubGraph/End) — no domain node type. The one Unity-domain action (`LoadSceneAction`) was justified in slice 1. Editor tooling is inherently Unity; gameflow is the host layer where that lives (spec 020 FR-010). |
| III. Specification-First | ✅ PASS | spec.md approved (16/16 checklist). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS (with editor boundary) | Tests-first for the testable surface: `GameFlowGraph`/`LoadSceneAction` carry `[CreateAssetMenu]`; the `GameFlowSampleBuilder` produces the exact reference structure AND runs under a `GraphFlowDriver` with a recording loader (A→await→B→end). Pure visual interaction (drag nodes, draw edges) is validated by the sample opening — the same boundary the sibling package editors use. |
| V. Simplicity (YAGNI) | ✅ PASS | Reuse graphcore's whole editor base; add only gameflow subclasses + the await/wait inspector fields + the sample. NO in-editor runner toolbar (deferred), NO custom component inspector, NO statement-label subclass (use `_title`). |
| VI. Typed Context Contract | ✅ PASS (N/A) | No new context usage; the editor authors data, it does not run a context. `GameFlowContext` (slice 1) is unchanged. |
| VII. Cross-lib via SubGraph only | ✅ PASS | Editor depends only on graphcore (Runtime + Editor) + gameflow Runtime. SubGraph authoring uses graphcore's `SubGraphNodeData` → a `BaseGraph` reference, the sanctioned mechanism. |
| Dev standards | ✅ PASS | `[GraphGameFlow]` prefix; one class per file; node-view styling via USS (reuse graphcore's stylesheet); XML docs. MonoBehaviour/UnityEvent rules N/A (editor tooling). |

**Result**: PASS — no violations. The only nuance (editor views aren't unit-tested) is the ecosystem's
established boundary, and the data/sample surface IS test-driven.

## Project Structure

### Documentation (this feature)

```text
specs/021-gameflow-editor-authoring/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/public-api.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
com.faolline.graphgameflow/
├── package.json                                   # 0.1.0 → 0.2.0
├── Runtime/
│   ├── Graph/
│   │   └── GameFlowGraph.cs                        # NEW: [CreateAssetMenu] BaseGraph subclass
│   └── Scene/LoadSceneAction.cs                    # MODIFIED: + [CreateAssetMenu]
├── Editor/
│   ├── com.faolline.graphgameflow.Editor.asmdef    # NEW (refs graphcore.Runtime+Editor, gameflow.Runtime)
│   ├── Window/GameFlowGraphEditorWindow.cs         # BaseGraphEditorWindow; [OnOpenAsset] + menu; toolbar (Save+Validate)
│   ├── Graph/GameFlowGraphView.cs                  # BaseGraphView; CreateNodeView switch + contextual "Add" menu
│   ├── Inspector/GameFlowNodeInspectorView.cs      # BaseNodeInspectorView; AddBaseNodeSection + Flow foldout (await/wait) + End/Choice/SubGraph sections
│   ├── Edges/GameFlowEdgeView.cs                    # BaseEdgeView
│   ├── Nodes/StartNodeView.cs
│   ├── Nodes/StatementNodeView.cs
│   ├── Nodes/ChoiceNodeView.cs
│   ├── Nodes/SubGraphNodeView.cs
│   ├── Nodes/EndNodeView.cs
│   └── Samples/GameFlowSampleBuilder.cs            # menu → reference scene-flow asset (+ LoadSceneAction sub-assets)
└── Tests/EditMode/
    ├── com.faolline.graphgameflow.Tests.EditMode.asmdef   # MODIFIED: + Editor + graphcore.Editor refs
    ├── GameFlowGraphTests.cs                        # NEW: GameFlowGraph is a BaseGraph + has [CreateAssetMenu]
    ├── LoadSceneActionTests.cs                      # MODIFIED: + has [CreateAssetMenu]
    └── GameFlowSampleBuilderTests.cs                # NEW: sample structure + runs A→await→B→end under the driver

# com.faolline.graphcore/  and  com.faolline.graphstandard/ : UNCHANGED.
# gameflow Runtime slice-1 (GraphFlowDriver, ISceneLoader, GameFlowContext): UNCHANGED.
```

**Structure Decision**: a new `Editor` assembly mirroring `com.faolline.starterGraph/Editor` one-for-one
(window, graph view, node views, inspector, edge view, sample builder), adapted to gameflow naming and the
Create-menu paths, plus the `GameFlowGraph` runtime subclass and the `LoadSceneAction` attribute. Everything
visual subclasses graphcore's editor base; the gameflow-specific logic is the `CreateNodeView` switch, the
contextual add-node menu, the inspector's await/wait fields, and the sample builder.

## Phase 0 — Research

See [research.md](research.md): R1 reuse `AddBaseNodeSection` for actions/conditions, add only a Flow foldout
(`_awaitSignal`, `_waitDuration`) — the LoadSceneAction attach is free; R2 author the universal node types
directly (name via `_title`, no statement-label subclass); R3 no in-editor runner toolbar (driver+Play is the
run path) — toolbar is Save + Validate only; R4 `GameFlowSampleBuilder` mirrors `StarterSampleBuilder`
(sub-asset actions, GUID ids, unique asset path); R5 the test boundary (data/attributes/sample-run are
EditMode-tested; views validated by the sample opening, as in sibling editors); R6 `GameFlowGraph : BaseGraph`
keeps the driver (which references `BaseGraph`) unchanged.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md),
  [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD — tests before code)

1. **Runtime surface (test-first)**: failing `GameFlowGraphTests` (GameFlowGraph is a `BaseGraph`; carries
   `[CreateAssetMenu]`) + add the `[CreateAssetMenu]` assertion to `LoadSceneActionTests` → create
   `Runtime/Graph/GameFlowGraph.cs` and add the attribute to `LoadSceneAction`. RED → GREEN.
2. **Sample builder (test-first, the meaty one)**: failing `GameFlowSampleBuilderTests` (the built graph has
   start + two scene-load statement nodes with attached `LoadSceneAction`s + an `AwaitSignalName="advance"`
   node + end + the four edges; and driving it with a `GraphFlowDriver` + recording loader walks A → await →
   B → end) → create `Editor/Samples/GameFlowSampleBuilder.cs` + the new Editor asmdef + update the test
   asmdef to reference Editor. RED → GREEN.
3. **Editor views (mirror starterGraph, validated by compile + sample open)**: `GameFlowGraph` subclass-aware
   `GameFlowGraphEditorWindow` ([OnOpenAsset] + menu, Save+Validate toolbar), `GameFlowGraphView`
   (CreateNodeView switch over the 5 universal types + contextual add-node menu + CreateEdgeView +
   OnNodeCreated entry-node default), the five node views, `GameFlowEdgeView`, and
   `GameFlowNodeInspectorView` (`AddBaseNodeSection` + a "Flow" foldout binding `_awaitSignal`/`_waitDuration`
   + End-reason / SubGraph-target / Choice sections mirrored from starter). No new unit tests for the views;
   they must compile and the sample must open in the window.
4. **Back-compat + finalize**: run the entire existing suite (654 EditMode incl. slice-1 gameflow + the new
   editor tests; graphcore/graphstandard untouched) green; the slice-1 8 PlayMode stay green; bump package
   0.2.0; update README (authoring section) + CHANGELOG; verify `[GraphGameFlow]` prefix + XML docs + USS.

## Complexity Tracking

> No violations — empty. (The editor-views-not-unit-tested boundary is the ecosystem norm, documented under
> Constitution Check IV and research R5, not a deviation requiring justification.)
