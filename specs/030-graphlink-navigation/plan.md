# Implementation Plan: Visual GraphLink cross-reference + editor navigation

**Branch**: `030-graphlink-navigation` | **Date**: 2026-06-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/030-graphlink-navigation/spec.md`

## Summary

Add a **documentary, editor-only** way to associate graphs: a new universal `GraphLinkNodeData` node (in
graphcore) that holds a `BaseGraph TargetGraph` reference plus an optional note, and is **never executed**
(the runner treats it as an inert pass-through). In the editor it renders as a distinct, labelled, openable
reference ("📎 Quest: Relics"); double-clicking routes through a graphcore **editor navigation registry**
(graph type → opener), populated opt-in by each lib's editor, to open the referenced graph in its proper
window — falling back to ping/select + a `[GraphCore]` diagnostic when no opener is registered. Zero runtime
behaviour change; graphcore stays universal (the registry holds delegates the libs register, no downstream refs).

## Technical Context

**Language/Version**: C# 9 / Unity 6000.3.6f1.

**Primary Dependencies**: `com.faolline.graphcore` (Runtime: `BaseNodeData`/`BaseRunner`; Editor:
`BaseNodeView`, `UnityEditor.Experimental.GraphView`, `EditorWindow`). Downstream editor opt-in:
graphquest / graphdialoguesystem / graphgameflow editors register their window.

**Storage**: N/A. `GraphLinkNodeData.TargetGraph` is a serialized `BaseGraph` (ScriptableObject) reference,
exactly like `SubGraphNodeData.TargetGraph`.

**Testing**: EditMode (NUnit) via Unity batchmode (`-runTests -testPlatform EditMode`). No PlayMode required.

**Target Platform**: Unity Editor (navigation) + graphcore Runtime (the inert pass-through contract).

**Project Type**: Unity package library (graphcore) + per-lib editor registrations.

**Performance Goals**: runtime pass-through is an O(1) no-op; registry lookup is a dictionary get. No hot path.

**Constraints**: ZERO change to existing runtime behaviour, existing SubGraph nesting, and all current tests
stay green. graphcore references nothing downstream (Principle II). `[GraphCore]` log prefix; one class per
file; `Action<T>` not `UnityEvent`; XML docs; USS for the node view.

**Scale/Scope**: 1 runtime node type + 1 runner pass-through branch; 1 editor node view + 1 editor registry +
1 open action; 3 one-line lib editor registrations; ~6 EditMode tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Foundation Stability** — PASS. Adds a NEW built-in node type (`GraphLinkNodeData`) and new public editor
  APIs → MINOR. `BaseNodeData` is untouched (append-only respected; `GraphLinkNodeData` is a new subclass).
  No public API removed.
- **II. Universal Abstractions Only** — PASS. `GraphLinkNodeData` holds a `BaseGraph` (never a typed lib graph)
  and references nothing downstream. The navigation registry maps `System.Type → opener delegate` and is
  *populated by the libs*; graphcore keeps zero knowledge of any specific lib. A graph-to-graph documentary
  reference + an editor-window registry are universal authoring concerns.
- **III. Specification-First** — PASS. `spec.md` approved (checklist all green) before this plan.
- **IV. Test-Driven Development** — PASS (enforced in tasks): EditMode tests written first — GraphLink off-path
  never entered; GraphLink on-path is a no-op pass-through (identical run); registry resolves a registered type
  and falls back gracefully for an unregistered one.
- **V. Simplicity (YAGNI)** — PASS. Thin node (a reference + note) + a small static registry (dictionary of
  delegates). No new abstraction beyond what the spec requires. Reuses the existing `BaseGraph` reference shape
  and the `BaseNodeView`/registry patterns already in graphcore (mirrors `NodeTypeColorRegistry`).
- **VI. Typed Context Contract** — N/A. `GraphLinkNodeData` is inert; it never touches `BaseContext` at runtime,
  so no typed-context subclass/keys are required.
- **VII. Cross-lib Compatibility via SubGraph Only** — PASS (no conflict). Principle VII governs **invocation**
  (one graph *executing* another) — which MUST stay SubGraph-only. `GraphLink` invokes/executes NOTHING; it is a
  non-executing documentary annotation. It still follows VII's letter (it references a `BaseGraph`, never a typed
  lib graph). The registry's editor opening is an Editor-time navigation action, not graph invocation.

**Result: PASS — no violations, no Complexity Tracking entries needed.**

## Project Structure

### Documentation (this feature)

```text
specs/030-graphlink-navigation/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions
├── data-model.md        # Phase 1 — entities
├── quickstart.md        # Phase 1 — author + lib-registration walkthrough
├── contracts/
│   └── graphlink-api.md # Phase 1 — public API + runtime/editor contracts
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── Runtime/
│   ├── Nodes/
│   │   └── GraphLinkNodeData.cs          # NEW — inert reference node (TargetGraph + Note + NodeTypeId)
│   └── Execution/
│       └── BaseRunner.cs                 # MODIFIED — pass-through branch for GraphLinkNodeData (no pause/exec)
├── Editor/
│   ├── Nodes/
│   │   └── GraphLinkNodeView.cs          # NEW — distinct labelled view; double-click → registry.Open
│   ├── Registry/
│   │   └── GraphEditorWindowRegistry.cs  # NEW — Type→opener; Register / Open(BaseGraph) + graceful fallback
│   └── Styles/
│       └── GraphLinkNodeView.uss         # NEW — distinct annotation styling (USS only)
└── Tests/EditMode/
    ├── GraphLinkRunnerPassThroughTests.cs        # NEW — off-path inert + on-path no-op pass-through
    └── Editor/GraphEditorWindowRegistryTests.cs  # NEW — resolve registered type + graceful fallback

com.faolline.graphquest/Editor/          # MODIFIED — register QuestGraph → quest window ([InitializeOnLoadMethod])
com.faolline.graphdialoguesystem/Editor/ # MODIFIED — register DialogueGraph → dialogue window
com.faolline.graphgameflow/Editor/       # MODIFIED — register GameFlowGraph → gameflow window
```

**Structure Decision**: Single Unity-package layout (graphcore owns the node + registry; downstream editors
register opt-in). This mirrors the existing graphcore Runtime/Editor split and the `NodeTypeColorRegistry`
opt-in pattern. Version bumps: graphcore MINOR (new node type + editor APIs); graphquest/dialogue/gameflow PATCH
(one registration line each) with their graphcore floor aligned per convention.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
