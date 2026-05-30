# Implementation Plan: GraphTest Verification Package

**Branch**: `005-graphtest-package` | **Date**: 2026-05-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-graphtest-package/spec.md`

## Summary

Create `com.faolline.graphTest` — a concrete downstream package that serves as the primary integration
test vehicle for `com.faolline.graphcore`. It provides a fully working graph editor window with three
concrete node types, a split-pane inspector, in-editor execution via `BaseRunner`, and an EditMode
test suite. Development surfaces one graphcore API gap (a missing `AddNodeToCanvas` protected helper
on `BaseGraphView`) that is fixed as a graphcore sub-task before the package implementation proceeds.

---

## Technical Context

**Language/Version**: C# 9 (Unity 6000.x)

**Primary Dependencies**: `com.faolline.graphcore` (local package), `com.unity.graphview` (Editor only)

**Storage**: Unity `ScriptableObject` assets serialized to `.asset` files on disk

**Testing**: NUnit via Unity Test Runner (EditMode only)

**Target Platform**: Unity Editor (EditorWindow + EditMode tests; no Play Mode, no MonoBehaviour)

**Project Type**: Unity Editor-extension package — downstream lib consuming `com.faolline.graphcore`

**Performance Goals**: Editor window opens in under 2 seconds; 3-node linear graph executes in under 1 second

**Constraints**: No `MonoBehaviour` in Runtime layer; no `UnityEvent`; no `com.unity.localization`;
EditMode tests only; no ecosystem lib cross-dependencies

**Scale/Scope**: 3 concrete node types, 1 concrete edge view, 1 editor window, 1 inspector panel,
EditMode test suite covering Runtime and Editor layers

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | `com.faolline.graphTest` is a downstream lib. The one graphcore change (protected `AddNodeToCanvas`) is a MINOR addition — no existing public API is removed or modified. |
| II – Universal Abstractions Only | ✅ Pass | Domain-specific types (`TestStatementNodeData.Label`) stay inside the test package; graphcore remains domain-free. |
| III – Specification-First | ✅ Pass | `spec.md` written and approved before this plan. |
| IV – Test-Driven Development | ✅ Pass | Tests MUST be written before implementation. Red-Green-Refactor is mandatory. Tests confirmed failing via Coplay `run_tests` before implementation begins. |
| V – Simplicity (YAGNI) | ✅ Pass | Simplest possible concrete implementations chosen. No extra abstractions. Complexity Tracking table is empty — no violations. |
| VI – Cross-lib Compatibility | ✅ Pass | No dependency on other ecosystem libs (`dialoguesystem`, `gameflow`, etc.). |

---

## Phase 0: Research

### Research Findings

**R-001 — Graphcore API Gap: `BaseGraphView.AddNodeToCanvas`**

- **Decision**: Add a `protected void AddNodeToCanvas(BaseNodeData nodeData, Vector2 position)` method to `BaseGraphView` in graphcore as a MINOR semver change.
- **Rationale**: `_graph`, `_nodeViews`, and `_isDirty` are private in `BaseGraphView`. A concrete subclass has no way to add a node from its context-menu override without reaching into private state. The three-line helper (`graph.AddNode`, `AddElement`, `_nodeViews[id] = view`) belongs in the base class alongside `LoadGraph` and `SaveGraph`.
- **Alternatives considered**: Making `_graph` and `_nodeViews` `protected` — rejected because it over-exposes mutable internal state and violates encapsulation. Providing a `protected void AddNodeToCanvas(...)` is the minimal, coherent surface.

**R-002 — Port Typing for Edge Creation**

- **Decision**: Node view ports are created with `Port.Create<TestEdgeView>(...)`. `TestEdgeView : BaseEdgeView` is the concrete edge visual for the test package.
- **Rationale**: Unity's GraphView creates edges by instantiating the generic type parameter of the port. If ports use the base `Edge` type, the dragged connection is never a `BaseEdgeView` and `HandleEdgeCreation`'s cast silently drops every connection. Typed ports is the required pattern.
- **Alternatives considered**: Overriding `graphViewChanged` to intercept raw `Edge` instances and re-wrap them — rejected as fragile and at odds with the existing `HandleEdgeCreation` pipeline.

**R-003 — Context Menu Node Creation**

- **Decision**: `TestGraphView` overrides `BuildContextualMenu(ContextualMenuPopulateEvent)` to add one menu entry per node type. Each entry calls the new `AddNodeToCanvas` helper (R-001).
- **Rationale**: This is the standard Unity GraphView extension point. No graphcore change needed beyond R-001.

**R-004 — In-Editor Execution (Run Button)**

- **Decision**: The `TestGraphEditorWindow` Run button creates a `BaseRunner`, calls `Start(graph, context)`, then iterates `Proceed()` until `State == Ended`, logging each node's `NodeType`/`Label` to `Debug.Log`. A `BaseContext` with no parameters is sufficient for the test runner.
- **Rationale**: `BaseRunner` is headless and runs fine in EditMode. No executor registration is needed for a simple traversal-only run; nodes with no registered executor are traversed without side effects, which is valid for verification purposes.
- **Alternatives considered**: Async execution across frames — rejected per Constitution Principle V (YAGNI) and the <1 s SC-004 success criterion.

**R-005 — No Custom Edge Data Type Needed**

- **Decision**: Use `BaseEdgeData` directly. No `TestEdgeData` subclass.
- **Rationale**: The test package does not add any domain-specific fields to edges. `BaseEdgeData` already carries `FromNodeId`, `ToNodeId`, `PortName`, and an optional condition — sufficient for all test scenarios.

---

## Phase 1: Design & Contracts

### Data Model

**[data-model.md](data-model.md)**

See separate file for full entity definitions.

---

### Project Structure

#### Documentation (this feature)

```text
specs/005-graphtest-package/
├── plan.md              ← this file
├── research.md          ← inline above (R-001…R-005)
├── data-model.md        ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit-tasks)
```

#### Source Code

```text
com.faolline.graphTest/
├── package.json
├── Runtime/
│   ├── TestGraph.cs                        ← BaseGraph subclass; [CreateAssetMenu]
│   ├── Nodes/
│   │   └── TestStatementNodeData.cs        ← StatementNodeData + Label field
│   └── com.faolline.graphTest.Runtime.asmdef
├── Editor/
│   ├── Graph/
│   │   └── TestGraphView.cs                ← BaseGraphView; context menu; Run execution
│   ├── Edges/
│   │   └── TestEdgeView.cs                 ← BaseEdgeView (required for typed ports)
│   ├── Nodes/
│   │   ├── StartNodeView.cs                ← BaseNodeView for StartNodeData
│   │   ├── TestStatementNodeView.cs        ← BaseNodeView for TestStatementNodeData
│   │   └── EndNodeView.cs                  ← BaseNodeView for EndNodeData
│   ├── Inspector/
│   │   └── TestNodeInspectorView.cs        ← BaseNodeInspectorView; type-switch on node type
│   ├── Window/
│   │   └── TestGraphEditorWindow.cs        ← BaseGraphEditorWindow; Save + Run toolbar
│   └── com.faolline.graphTest.Editor.asmdef
└── Tests/
    └── EditMode/
        ├── Runtime/
        │   ├── TestStatementNodeDataTests.cs
        │   └── TestGraphTests.cs
        ├── Editor/
        │   └── TestGraphViewAddNodeTests.cs
        └── com.faolline.graphTest.Tests.EditMode.asmdef
```

#### Graphcore sub-task (required before test-package implementation)

```text
com.faolline.graphcore/
└── Editor/
    └── Graph/
        └── BaseGraphView.cs    ← ADD: protected AddNodeToCanvas(BaseNodeData, Vector2)
```

---

### Contracts

This package is a verification/reference tool, not a public library — no external API contract is defined. It has no consumers.

---

### Key Design Decisions

**D-001 — `TestStatementNodeData.Label`**
`TestStatementNodeData` extends `StatementNodeData` and adds a single `[SerializeField] private string _label` with a public `Label` getter/setter. This is the only domain-specific field; it gives the inspector panel a meaningful editable property to exercise and survive save/reload.

**D-002 — Port creation pattern**
All ports in `StartNodeView`, `TestStatementNodeView`, and `EndNodeView` are created with `Port.Create<TestEdgeView>(...)`. Output ports use `Port.Capacity.Single`; the Start node has one output port named `"out"`. `TestStatementNodeView` has one input port `"in"` and one output port `"out"`. `EndNodeView` has one input port `"in"`.

**D-003 — `TestGraphView.CreateNodeView` dispatch**
A `switch` on `node.NodeType` returns the correct view subclass:
- `StartNodeData.NodeTypeId` → `new StartNodeView(node as StartNodeData)`
- `TestStatementNodeData.NodeTypeId` → `new TestStatementNodeView(node as TestStatementNodeData)`
- `EndNodeData.NodeTypeId` → `new EndNodeView(node as EndNodeData)`
- fallback → `null` (node silently skipped, matching the `if (view == null) continue` guard already in `BaseGraphView.LoadGraph`)

**D-004 — Inspector type dispatch**
`TestNodeInspectorView.BindNode` uses a type switch:
- Always renders the shared base-node section via `AddBaseNodeSection`.
- For `TestStatementNodeData`, also renders a `PropertyField` for `_label` above the base section.

**D-005 — Run button execution loop**
```
1. Guard: graph == null → Debug.LogError("[GraphTest] No graph loaded.")
2. Guard: no StartNodeData in graph.Nodes → Debug.LogError("[GraphTest] Graph has no Start node.")
3. Create new BaseContext(); new BaseRunner()
4. runner.Start(graph, context)
5. while runner.State == NodeReady: Debug.Log(current node type + label); runner.Proceed()
6. Debug.Log("[GraphTest] Graph ended: " + runner.EndReason)
```
`BaseRunner.CurrentNode` is read for logging; execution is synchronous in a single Editor frame.

**D-006 — `TestGraphEditorWindow` opening**
A `[MenuItem("Faolline/Open TestGraph Editor")]` opens the window. A separate `[OnOpenAsset]` callback loads the double-clicked `TestGraph` asset into the window automatically.

---

## Complexity Tracking

No constitution violations. No entries required.
