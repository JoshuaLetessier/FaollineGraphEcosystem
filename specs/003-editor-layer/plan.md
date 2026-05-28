# Implementation Plan: GraphCore Editor Layer

**Branch**: `003-editor-layer` | **Date**: 2026-05-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-editor-layer/spec.md`

## Summary

This feature builds the editor layer of `com.faolline.graphcore` on top of the data and
execution layers delivered in `001-data-layer` and `002-execution-runtime`. It delivers
five cohesive components in a new editor-only assembly:

1. **`BaseGraphView`** — extends Unity's `GraphView`; routes `graphViewChanged` callbacks
   to three protected virtual hooks (`OnNodeCreated`, `OnEdgeConnected`, `OnNodeDeleted`);
   implements one-way save-only data sync; split across two partial files (core +
   copy/paste).
2. **`BaseNodeView` + `BaseEdgeView`** — abstract visual nodes and edges with a sealed
   three-step color resolution chain: `HasColorOverride` → `NodeTypeColorRegistry` →
   graphcore default grey (`#808080`).
3. **`BaseGraphEditorWindow`** — abstract `EditorWindow` that hosts a `BaseGraphView` and
   provides a Save button wired to `BaseGraphView.SaveGraph()`.
4. **`CycleDetector`** — stateless iterative DFS over `SubGraphNodeData.TargetGraph`
   asset references; called on every `OnEdgeConnected` without exception; visually
   refuses cyclic connections with a `[GraphCore]`-prefixed error.
5. **`NodeTypeColorRegistry`** — static color map populated by downstream libs in
   `[InitializeOnLoad]` constructors; consumed by the `ResolveColor()` chain.

Semver assessment: **MINOR** bump (0.2.0 → 0.3.0) — new public API in a new
editor-only assembly; no existing public API is removed or broken.

## Technical Context

**Language/Version**: C# 9 (Unity 6000.x Roslyn compiler)

**Primary Dependencies**:
- `com.faolline.graphcore.Runtime` (001-data-layer + 002-execution-runtime assemblies)
- `UnityEditor.Experimental.GraphView` (Unity 6000.x, Editor only — permitted by constitution)
- `UnityEditor` engine assemblies (Editor only)

**Storage**: ScriptableObject serialization via `EditorUtility.SetDirty` +
`AssetDatabase.SaveAssets` on explicit save only

**Testing**: Unity Test Runner, EditMode only
(`com.faolline.graphcore.Tests.EditMode.asmdef`, extended with Editor assembly reference)

**Target Platform**: Unity Editor only (assembly `includePlatforms: ["Editor"]`)

**Project Type**: Library (editor extension)

**Performance Goals**: Human-interaction speed — no frame-rate or throughput targets.
`CycleDetector` DFS is bounded by the number of `BaseGraph` assets in the project
(practical bound: hundreds, not millions).

**Constraints**:
- No inline C# style assignments — USS only (enforced by FR-006)
- No `MonoBehaviour` or `UnityEvent` anywhere in the editor assembly
- Save-only data sync — no writes to `BaseGraph` during node movement
- `partial class` permitted for `BaseGraphView` only (constitution Development Standards)
- No ecosystem lib references in the editor assembly

**Scale/Scope**: New editor assembly; ~10 new source files + 3 USS files; ~600 LOC estimated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | New editor-only assembly; zero Runtime changes. MINOR bump (0.2.0 → 0.3.0). No existing public API removed. |
| II. Universal Abstractions Only | ✅ PASS | `BaseGraphView`, `BaseNodeView`, `BaseEdgeView`, `CycleDetector` are universal editor abstractions — no domain semantics (no dialogue, no quest). |
| III. Specification-First | ✅ PASS | `spec.md` written and approved before `plan.md`. |
| IV. Test-Driven Development | ✅ PASS | TDD enforced in tasks. `CycleDetector` and `NodeTypeColorRegistry` are pure logic — fully testable before visual implementation. `BaseNodeView.ResolveColor()` is sealed and independently testable. |
| V. Simplicity (YAGNI) | ✅ PASS | Static `Dictionary` for `NodeTypeColorRegistry` (not ScriptableObject, not IoC). Iterative DFS for `CycleDetector` (not Kahn's). `partial` used only where constitution explicitly allows. See research.md for rejected alternatives. |
| VI. Cross-lib Compatibility via SubGraph Only | ✅ PASS | `CycleDetector` follows `SubGraphNodeData.TargetGraph` references only — no knowledge of lib-specific graph types. Edit-time cycle detection satisfies the mandatory requirement. |

**Pre-implementation gate**: PASSED. All six principles satisfied. No violations requiring
justification.

*Post-design re-check*: PASSED. Phase 1 data model introduces no new violations.
`partial class` scope is limited to `BaseGraphView` per the constitution. `NodeTypeColorRegistry`
remains a static dictionary (YAGNI confirmed).

## Project Structure

### Documentation (this feature)

```text
specs/003-editor-layer/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── public-api.md    # Phase 1 output — public C# interface surface
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code

```text
Editor/
├── Graph/
│   ├── BaseGraphView.cs              # NEW — core canvas + hooks
│   └── BaseGraphView.CopyPaste.cs    # NEW — partial: copy/paste with GUID reassignment
├── Window/
│   └── BaseGraphEditorWindow.cs      # NEW — EditorWindow host
├── Nodes/
│   └── BaseNodeView.cs               # NEW — abstract node view + color resolution
├── Edges/
│   └── BaseEdgeView.cs               # NEW — abstract edge view + color resolution
├── Tools/
│   ├── CycleDetector.cs              # NEW — iterative DFS over asset dependency graph
│   └── CycleDetectionResult.cs       # NEW — readonly struct for DFS result
├── Registry/
│   ├── NodeTypeColorRegistry.cs      # NEW — static color map
│   └── GraphCoreDefaults.cs          # NEW — shared constants (NodeGrey)
├── Clipboard/
│   └── GraphClipboardData.cs         # NEW — intermediate clipboard model
├── Resources/
│   └── GraphCore/
│       ├── GraphCoreEditor.uss       # NEW — canvas base styles
│       ├── BaseNodeView.uss          # NEW — node chrome styles
│       └── BaseEdgeView.uss          # NEW — edge styles
└── com.faolline.graphcore.Editor.asmdef  # NEW

Tests/
└── EditMode/
    ├── Editor/
    │   ├── CycleDetectorTests.cs          # NEW
    │   ├── NodeTypeColorRegistryTests.cs  # NEW
    │   ├── BaseNodeViewColorTests.cs      # NEW
    │   ├── BaseEdgeViewColorTests.cs      # NEW
    │   └── CopyPasteGuidTests.cs          # NEW
    └── com.faolline.graphcore.Tests.EditMode.asmdef  # UPDATE: add Editor assembly reference
```

**Structure Decision**: Editor code lives in a dedicated `Editor/` root folder (parallel to
`Runtime/` and `Tests/`) under a new `com.faolline.graphcore.Editor.asmdef`. The `partial`
split for `BaseGraphView` uses the `ClassName.Aspect.cs` naming convention. USS files are
co-located under `Editor/Resources/GraphCore/` and loaded via `AssetDatabase.LoadAssetAtPath`.

Pure-logic types (`CycleDetector`, `NodeTypeColorRegistry`, `GraphCoreDefaults`) are grouped
in topology-named subfolders (`Tools/`, `Registry/`) matching the principle of one class per
file and clear separation of concerns.

## Complexity Tracking

> No constitution violations. Section not required.
