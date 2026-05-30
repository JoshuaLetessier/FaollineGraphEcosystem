# Tasks: GraphTest Verification Package

**Input**: Design documents from `specs/005-graphtest-package/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | data-model.md ✅

**TDD Note**: Constitution Principle IV mandates Red-Green-Refactor. Every test task
MUST be run via Coplay `run_tests` to confirm failure BEFORE the matching implementation task begins.

**Organization**: Tasks are grouped by user story. Each phase is independently testable.

---

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase
- **[Story]**: User story this task belongs to (US1–US5)

---

## Phase 1: Setup (Package Scaffolding)

**Purpose**: Create the `com.faolline.graphTest` package skeleton. No implementation logic yet.

- [X] T001 Create directory tree for `com.faolline.graphTest/` (Runtime/Nodes, Editor/Graph, Editor/Edges, Editor/Nodes, Editor/Inspector, Editor/Window, Tests/EditMode/Runtime, Tests/EditMode/Editor)
- [X] T002 [P] Create `com.faolline.graphTest/package.json` with name `com.faolline.graphTest`, version `0.1.0`, and dependency on `com.faolline.graphcore`
- [X] T003 [P] Create `com.faolline.graphTest/Runtime/com.faolline.graphTest.Runtime.asmdef` referencing `com.faolline.graphcore.Runtime`
- [X] T004 [P] Create `com.faolline.graphTest/Editor/com.faolline.graphTest.Editor.asmdef` referencing `com.faolline.graphcore.Runtime` and `com.faolline.graphcore.Editor` (Editor-only)
- [X] T005 [P] Create `com.faolline.graphTest/Tests/EditMode/com.faolline.graphTest.Tests.EditMode.asmdef` referencing Runtime, Editor, and both graphcore assemblies (test platform: Editor)

**Checkpoint**: Package compiles with empty assemblies. No errors in Unity console.

---

## Phase 2: Foundational — Graphcore Sub-Tasks

**Purpose**: Two MINOR additive changes to `com.faolline.graphcore` required before user story work can begin.
**⚠️ CRITICAL**: No user story implementation can begin until T006–T011 are complete and all tests green.

### Sub-task A — `BaseGraphView.AddNodeToCanvas`

Without this protected helper, the concrete `TestGraphView` has no way to add nodes from its
context menu (private `_graph`, `_nodeViews`, and `_isDirty` fields block access from subclasses).

- [X] T006 Write failing EditMode test `AddNodeToCanvas_AddsNodeToGraphAndCanvas` in `com.faolline.graphcore/Tests/EditMode/Editor/BaseGraphViewAddNodeTests.cs` — asserts calling the (not-yet-existing) method adds the node to the loaded graph's `Nodes` list. Run via Coplay `run_tests` and confirm failure before T007.
- [X] T007 Add `protected void AddNodeToCanvas(BaseNodeData nodeData, Vector2 position)` to `com.faolline.graphcore/Editor/Graph/BaseGraphView.cs` — sets `nodeData.Id` (GUID) and `nodeData.NodeType` if blank, calls `_graph.AddNode(nodeData)`, creates the view via `CreateNodeView`, sets position, registers in `_nodeViews`, calls `AddElement(view)`, sets `_isDirty = true`, calls `OnNodeCreated(nodeData)`.
- [X] T008 Run T006 test suite via Coplay `run_tests` and confirm all new tests pass. Fix any regressions in existing graphcore editor tests before proceeding.

### Sub-task B — `BaseGraphEditorWindow.PopulateToolbar`

Without a hook, the concrete window cannot add a Run button to the toolbar (the toolbar local
variable is not accessible from subclasses).

- [X] T009 Write failing EditMode test `PopulateToolbar_CalledDuringBuildToolbar` in `com.faolline.graphcore/Tests/EditMode/Editor/BaseGraphEditorWindowPopulateToolbarTests.cs` — subclass overrides `PopulateToolbar` and asserts it was invoked with a non-null `Toolbar` during `OnEnable`. Run via Coplay `run_tests` and confirm failure before T010.
- [X] T010 Add `protected virtual void PopulateToolbar(Toolbar toolbar)` to `com.faolline.graphcore/Editor/Window/BaseGraphEditorWindow.cs` — call it from `BuildToolbar` immediately after the Save button is added. Default implementation is empty (no-op).
- [X] T011 Run T009 test suite via Coplay `run_tests` and confirm all new tests pass. Fix any regressions before proceeding.

**Checkpoint**: Graphcore sub-tasks complete. Both new protected members exist and tests are green.

---

## Phase 3: User Story 1 — Open the Graph Editor Window (Priority: P1) 🎯 MVP

**Goal**: A graph editor window can be opened via the Unity menu and displays a canvas with a Save toolbar button. No nodes yet — just a working empty window.

**Independent Test**: Open the window via `Faolline/Open TestGraph Editor`, confirm it appears with a canvas and toolbar, close it. Zero console errors.

### Tests for US1

- [X] T012 [US1] Write failing EditMode test `TestGraph_IsBaseGraphSubclass` and `TestGraph_HasCreateAssetMenuAttribute` in `com.faolline.graphTest/Tests/EditMode/Runtime/TestGraphTests.cs`. Run via Coplay `run_tests` and confirm failure before T013.

### Implementation for US1

- [X] T013 [US1] Create `com.faolline.graphTest/Runtime/TestGraph.cs` — `TestGraph : BaseGraph` with `[CreateAssetMenu(menuName = "GraphTest/Test Graph", fileName = "NewTestGraph")]`.
- [X] T014 [US1] Create `com.faolline.graphTest/Editor/Edges/TestEdgeView.cs` — `TestEdgeView : BaseEdgeView` with a default constructor that calls `Initialize(null)`. No extra logic; serves as the typed port edge class.
- [X] T015 [US1] Create `com.faolline.graphTest/Editor/Graph/TestGraphView.cs` — `TestGraphView : BaseGraphView` with stub `CreateNodeView` (returns null for all types) and `CreateEdgeView` (returns `new TestEdgeView()`). No context menu yet.
- [X] T016 [US1] Create `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs` — `TestGraphEditorWindow : BaseGraphEditorWindow` implementing `CreateGraphView()` returning `new TestGraphView()`. Add `[MenuItem("Faolline/Open TestGraph Editor")]` static opener. Add `[OnOpenAsset]` callback that opens the window and calls `LoadGraph(asset)` when a `TestGraph` asset is double-clicked.

**Checkpoint**: `Faolline/Open TestGraph Editor` opens a window with empty canvas and Save button. T012 tests pass. Zero console errors.

---

## Phase 4: User Story 2 — Add Test Nodes to the Canvas (Priority: P1)

**Goal**: Right-clicking the canvas shows a context menu with three node types. Selecting one creates that node on the canvas. Nodes persist after save/reload.

**Independent Test**: Open window, load a `TestGraph` asset, right-click canvas, add one of each type, save, reopen — all three nodes present.

### Tests for US2

- [X] T017 [US2] Write failing EditMode tests for `TestStatementNodeData` in `com.faolline.graphTest/Tests/EditMode/Runtime/TestStatementNodeDataTests.cs`:
  - `TestStatementNodeData_NodeTypeId_IsCorrect` (expects `"graphtest/statement"`)
  - `TestStatementNodeData_Label_DefaultsToEmpty`
  - `TestStatementNodeData_Label_RoundTrips` (set → get)
  Run via Coplay `run_tests` and confirm failure before T018.
- [X] T018 [US2] Write failing EditMode test `AddNodeToCanvas_CreatesNodeViewAndRegistersInGraph` in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphViewAddNodeTests.cs` — loads a `TestGraph`, calls the context menu path for `TestStatementNodeData`, asserts the graph has one node and the view exists. Run via Coplay `run_tests` and confirm failure before T019.

### Implementation for US2

- [X] T019 [P] [US2] Create `com.faolline.graphTest/Runtime/Nodes/TestStatementNodeData.cs` — `TestStatementNodeData : StatementNodeData` with `public const string NodeTypeId = "graphtest/statement"` and a `[SerializeField] private string _label` with public `Label { get; set; }`.
- [X] T020 [P] [US2] Create `com.faolline.graphTest/Editor/Nodes/StartNodeView.cs` — `StartNodeView : BaseNodeView` with `OnBuildView()` adding one output port `"out"` typed `Port.Create<TestEdgeView>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool))`. Constructor calls `Initialize(nodeData)`.
- [X] T021 [P] [US2] Create `com.faolline.graphTest/Editor/Nodes/TestStatementNodeView.cs` — `TestStatementNodeView : BaseNodeView` with `OnBuildView()` adding input port `"in"` and output port `"out"`, both typed `Port.Create<TestEdgeView>(...)`. Display `Label` as a read-only `Label` element in the node body.
- [X] T022 [P] [US2] Create `com.faolline.graphTest/Editor/Nodes/EndNodeView.cs` — `EndNodeView : BaseNodeView` with `OnBuildView()` adding one input port `"in"` typed `Port.Create<TestEdgeView>(...)`. No output port.
- [X] T023 [US2] Update `TestGraphView.CreateNodeView` in `com.faolline.graphTest/Editor/Graph/TestGraphView.cs` — replace stub with type-switch dispatch: `StartNodeData.NodeTypeId → new StartNodeView(...)`, `TestStatementNodeData.NodeTypeId → new TestStatementNodeView(...)`, `EndNodeData.NodeTypeId → new EndNodeView(...)`, fallback `→ null`.
- [X] T024 [US2] Implement `TestGraphView.BuildContextualMenu` override in `com.faolline.graphTest/Editor/Graph/TestGraphView.cs` — adds three `DropdownMenuAction` entries ("Add Start Node", "Add Statement Node", "Add End Node"), each calling `AddNodeToCanvas` with the correct `BaseNodeData` subclass instance and the event's mouse position.

**Checkpoint**: Context menu shows all three node types. Nodes appear at click position. T017–T018 tests pass.

---

## Phase 5: User Story 3 — Connect Nodes with Edges (Priority: P1)

**Goal**: Dragging from an output port to an input port creates an edge. Edges persist after save/reload. Cyclic connections are rejected.

**Independent Test**: Add Start + TestStatement + End nodes, draw two edges (Start→Statement, Statement→End), save, reopen — both edges present. Attempt to draw Statement→Start — rejected.

### Tests for US3

- [X] T025 [US3] Write failing EditMode test `TestEdgeView_ImplementsBaseEdgeView` in `com.faolline.graphTest/Tests/EditMode/Runtime/TestGraphEdgeTests.cs`. Also add `TestGraph_CanAddAndRetrieveEdge` and `TestGraph_RemoveEdge_DecreasesEdgeCount`. Run via Coplay `run_tests` and confirm failure before T026.

### Implementation for US3

- [X] T026 [US3] Complete `com.faolline.graphTest/Editor/Edges/TestEdgeView.cs` — add `TestEdgeView(BaseEdgeData data)` constructor calling `Initialize(data)`. This is the constructor used by `CreateEdgeView`.
- [X] T027 [US3] Implement `TestGraphView.CreateEdgeView` in `com.faolline.graphTest/Editor/Graph/TestGraphView.cs` — replace stub with `return new TestEdgeView(edgeData)`.

**Checkpoint**: Edges can be drawn between compatible ports. Cycle rejection works (inherits from graphcore). T025 tests pass.

---

## Phase 6: User Story 4 — Inspect and Edit Node Properties (Priority: P2)

**Goal**: Clicking a node shows its editable properties in the right-hand inspector panel. Label edits on `TestStatementNodeData` persist after save/reload.

**Independent Test**: Select a `TestStatementNodeData` node — inspector shows "Label" field. Edit label to "hello", save, reopen, select node — inspector shows "hello".

### Tests for US4

- [X] T028 [US4] Write failing EditMode tests in `com.faolline.graphTest/Tests/EditMode/Editor/TestNodeInspectorViewTests.cs`:
  - `BindNode_WithTestStatementNodeData_DoesNotThrow`
  - `BindNode_WithStartNodeData_DoesNotThrow`
  - `ClearInspector_RemovesAllChildren`
  Run via Coplay `run_tests` and confirm failure before T029.

### Implementation for US4

- [X] T029 [US4] Create `com.faolline.graphTest/Editor/Inspector/TestNodeInspectorView.cs` — `TestNodeInspectorView : BaseNodeInspectorView`. `BindNode(BaseNodeData node)` clears first, then: if `TestStatementNodeData`, renders a `PropertyField` for `_label` via `FindNodeProperty`; always calls `AddBaseNodeSection` for universal fields. `ClearInspector()` clears all children.
- [X] T030 [US4] Override `CreateNodeInspectorView()` in `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs` — return `new TestNodeInspectorView()`.

**Checkpoint**: Split-pane layout visible. Selecting a node shows properties. Label edits persist. T028 tests pass.

---

## Phase 7: User Story 5 — Execute a Test Graph (Priority: P2)

**Goal**: Clicking Run in the toolbar executes the loaded graph synchronously, logging each visited node and the final end reason to the console.

**Independent Test**: Load a Start→TestStatement("hello")→End graph, click Run — console shows three log lines in order and a "Graph ended" line. Load a cyclic graph, click Run — cycle error logged, zero node-visit lines.

### Tests for US5

- [X] T031 [US5] Write failing EditMode test `ExecuteGraph_LinearChain_LogsAllNodes` in `com.faolline.graphTest/Tests/EditMode/Editor/TestGraphExecutionTests.cs` — builds a Start→TestStatement→End graph in memory, invokes the execution method directly (extracted to a testable helper), captures `Debug.Log` output, asserts three node-visit entries and one completion entry. Run via Coplay `run_tests` and confirm failure before T032.

### Implementation for US5

- [X] T032 [US5] Add `internal void ExecuteGraph(BaseGraph graph)` to `com.faolline.graphTest/Editor/Window/TestGraphEditorWindow.cs` — guards (null graph, no start node), creates `BaseRunner` + `BaseContext`, calls `runner.Start(graph, context, registry)`, loops `runner.Proceed()` while `State == NodeReady` logging via `OnNodeEntered` event, logs `[GraphTest] Graph ended: {endReason}` on completion.
- [X] T033 [US5] Override `PopulateToolbar(Toolbar toolbar)` in `TestGraphEditorWindow` — adds a `ToolbarButton` labelled "Run" that calls `ExecuteGraph(LoadedGraph)`. `LoadedGraph` is exposed as `protected BaseGraph LoadedGraph => _loadedGraph` in `BaseGraphEditorWindow` (MINOR graphcore addition, included in T010 scope).

> **Note**: If `_loadedGraph` is not accessible from the subclass, add `protected BaseGraph LoadedGraph => _loadedGraph` to `BaseGraphEditorWindow` as another MINOR graphcore change (add to T033 scope).

**Checkpoint**: Run button appears in toolbar. Linear graph executes and logs correctly. Cycle guard fires. T031 tests pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, XML comments, and final validation against all spec success criteria.

- [X] T034 [P] Add XML `<summary>` doc comments to all public members in `com.faolline.graphTest/Runtime/` (`TestGraph`, `TestStatementNodeData`)
- [X] T035 [P] Add XML `<summary>` doc comments to all public members in `com.faolline.graphTest/Editor/` (`TestGraphView`, `TestEdgeView`, `TestNodeInspectorView`, `TestGraphEditorWindow`, all node views)
- [ ] T036 Run full graphcore EditMode test suite via Coplay `run_tests` — all green, zero regressions from the Phase 2 graphcore changes
- [ ] T037 Run full `com.faolline.graphTest` EditMode test suite via Coplay `run_tests` — all green
- [ ] T038 Manual integration smoke test per spec success criteria: open window (SC-001), add all three node types (SC-002), save/reload (SC-003), run Start→Statement→End (SC-004), inspector edit+persist (SC-005), run cyclic graph (SC-006) — document results in a comment on this task when complete

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately. All tasks [P]-parallelizable.
- **Phase 2 (Foundational)**: Depends on Phase 1. **BLOCKS all user story phases.**
- **Phase 3 (US1)**: Depends on Phase 2 completion. No dependency on US2–US5.
- **Phase 4 (US2)**: Depends on Phase 2 + Phase 3 (`TestGraphView`, `TestGraph` must exist). `TestEdgeView` from T014 must exist before T020–T022.
- **Phase 5 (US3)**: Depends on Phase 4 (node views with typed ports must exist). Edge creation flows through graphcore's existing `HandleEdgeCreation` pipeline.
- **Phase 6 (US4)**: Depends on Phase 3 (window exists) and Phase 4 (node types exist). Independent of Phase 5.
- **Phase 7 (US5)**: Depends on Phase 3 (window), Phase 4 (nodes), Phase 2-B (`PopulateToolbar`). Independent of Phase 5–6.
- **Phase 8 (Polish)**: Depends on Phases 3–7 complete.

### User Story Dependencies

- **US1 (P1)**: Independent after Phase 2
- **US2 (P1)**: Needs US1 (window and `TestGraphView` scaffold)
- **US3 (P1)**: Needs US2 (node views with typed ports)
- **US4 (P2)**: Needs US1 (window) + US2 (node types) — independent of US3 and US5
- **US5 (P2)**: Needs US1 (window + `PopulateToolbar`) + US2 (node types for test graph)

### Within Each Phase

1. Write test → run via Coplay `run_tests` → confirm FAILURE
2. Implement → run via Coplay `run_tests` → confirm PASS
3. Commit atomically before moving to next task

---

## Parallel Opportunities

### Phase 1 (T002–T005): All four asmdef/package.json tasks in parallel

```
T002: package.json
T003: Runtime.asmdef
T004: Editor.asmdef
T005: Tests.asmdef
```

### Phase 4 (T020–T022): All three node views in parallel (different files)

```
T020: StartNodeView.cs
T021: TestStatementNodeView.cs
T022: EndNodeView.cs
```

### Phase 8 (T034–T035): Runtime and Editor XML doc in parallel

```
T034: Runtime XML docs
T035: Editor XML docs
```

---

## Implementation Strategy

### MVP (US1 only — Phase 1 + 2 + 3)

1. Phase 1: Create package skeleton
2. Phase 2: Add `AddNodeToCanvas` + `PopulateToolbar` to graphcore
3. Phase 3: `TestGraph` + `TestEdgeView` stub + `TestGraphView` stub + `TestGraphEditorWindow`
4. **STOP**: Open window via menu → verify empty canvas, no errors
5. Ship Phase 3 as the first verified increment

### Incremental Delivery

1. MVP → window opens ✅
2. Add Phase 4 → nodes appear in context menu ✅
3. Add Phase 5 → edges connect between nodes ✅
4. Add Phase 6 → inspector shows node properties ✅
5. Add Phase 7 → Run button executes the graph ✅
6. Phase 8 → polish and final validation ✅

---

## Notes

- Constitution Principle IV is non-negotiable: every implementation task has a corresponding prior test task
- Graphcore changes (T006–T011) must pass the graphcore test suite before any test-package work begins
- The `TestEdgeView` stub in T014 is intentionally minimal — just enough to unblock port typing in T020–T022; it is completed in T026
- `BaseEdgeData` is used directly without subclassing (see data-model.md R-005)
- If T033 requires exposing `_loadedGraph` from `BaseGraphEditorWindow`, that is a third MINOR graphcore change; scope it into T033 and document the semver bump accordingly
