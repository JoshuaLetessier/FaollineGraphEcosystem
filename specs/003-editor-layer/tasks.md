# Tasks: GraphCore Editor Layer

**Input**: Design documents from `specs/003-editor-layer/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/public-api.md ✅, quickstart.md ✅

**TDD Note**: Per constitution (Principle IV), tests MUST be written before implementation.
Tasks are ordered accordingly: for each testable component, the test task precedes the
implementation task. Run `Coplay MCP run_tests` after each test task to confirm RED before
implementing.

**Organization**: Tasks grouped by user story to enable independent implementation and
testing of each story.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US6) — omitted for Setup/Foundational/Polish phases

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Assembly structure, USS stubs, test infrastructure.

- [X] T001 Create Editor/ directory tree: Editor/Graph/, Editor/Window/, Editor/Nodes/, Editor/Edges/, Editor/Tools/, Editor/Registry/, Editor/Clipboard/, Editor/Resources/GraphCore/
- [X] T002 [P] Create Editor/com.faolline.graphcore.Editor.asmdef with name "com.faolline.graphcore.Editor", rootNamespace "Faolline.GraphCore.Editor", references ["com.faolline.graphcore.Runtime"], includePlatforms ["Editor"]
- [X] T003 [P] Create USS stubs with empty rule sets: Editor/Resources/GraphCore/GraphCoreEditor.uss, Editor/Resources/GraphCore/BaseNodeView.uss, Editor/Resources/GraphCore/BaseEdgeView.uss
- [X] T004 Create Tests/EditMode/Editor/ directory and update Tests/EditMode/com.faolline.graphcore.Tests.EditMode.asmdef to add the com.faolline.graphcore.Editor assembly GUID reference

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared logic building blocks (constants, registries, utilities) that all user
story phases depend on. Implement and prove correct before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add RemoveNode(BaseNodeData node) and RemoveEdge(BaseEdgeData edge) to Runtime/Graph/BaseGraph.cs — editor delete/undo paths require mutable graph; this is a MINOR Runtime API addition (no existing API broken)
- [X] T006 [P] Write failing tests in Tests/EditMode/Editor/NodeTypeColorRegistryTests.cs: Register stores color; Register twice → second replaces first; TryGet returns false for unknown type; Clear resets all registrations → run_tests → confirm RED
- [X] T007 [P] Write failing tests in Tests/EditMode/Editor/CycleDetectorTests.cs: unrelated graphs → no cycle; self-cycle (root == proposed) → cycle; indirect cycle A→B→C→A; null proposed → no cycle; non-SubGraph nodes → no cycle → run_tests → confirm RED
- [X] T008 [P] Write failing tests in Tests/EditMode/Editor/BaseNodeViewColorTests.cs using a concrete test-double subclass: HasColorOverride=true → ResolveColor returns override; HasColorOverride=false + registered type → registry color; HasColorOverride=false + no registry → NodeGrey (#808080) → run_tests → confirm RED
- [X] T009 [P] Write failing tests in Tests/EditMode/Editor/CopyPasteGuidTests.cs: pasted node GUIDs all differ from originals; pasted edge FromNodeId/ToNodeId reference new GUIDs only; paste twice from same data → two non-overlapping GUID sets → run_tests → confirm RED
- [X] T010 [P] Implement Editor/Registry/GraphCoreDefaults.cs (public static class; NodeGrey = new Color(0.502f, 0.502f, 0.502f))
- [X] T011 Implement Editor/Registry/NodeTypeColorRegistry.cs (static Dictionary<string,Color>; Register, TryGet, Clear) → run_tests → confirm T006 GREEN
- [X] T012 [P] Implement Editor/Tools/CycleDetectionResult.cs (public readonly struct; HasCycle bool; CyclePath IReadOnlyList<string>; constructor)
- [X] T013 Implement Editor/Tools/CycleDetector.cs (public static class; Check(BaseGraph root, BaseGraph proposed) iterative DFS over SubGraphNodeData.TargetGraph refs; returns CycleDetectionResult) → run_tests → confirm T007 GREEN
- [X] T014 [P] Implement Editor/Clipboard/GraphClipboardData.cs ([Serializable] public class; Nodes List<string>; Edges List<string>)

**Checkpoint**: T006 and T007 GREEN. CycleDetector and NodeTypeColorRegistry proven correct before visual integration.

---

## Phase 3: US1 - Open and Navigate (Priority: P1) 🎯 MVP

**Goal**: Developer opens a BaseGraphEditorWindow, loads a BaseGraph asset, and sees all
nodes and edges rendered. Pan, zoom, and node moves do NOT write to the asset.

**Independent Test**: Open window with a 3-node, 2-edge BaseGraph → 3 BaseNodeView instances
and 2 BaseEdgeView instances visible on canvas. Pan and zoom → EditorUtility.IsDirty(graph)
remains false.

### Tests for US1

- [X] T015 [US1] Extend Tests/EditMode/Editor/BaseNodeViewColorTests.cs with construction tests: create concrete test-double BaseNodeView stub → ResolveColor() returns NodeGrey; verify USS loaded via styleSheets.count > 0 → run_tests → confirm RED
- [X] T016 [P] [US1] Write failing tests in Tests/EditMode/Editor/BaseEdgeViewColorTests.cs using concrete test-double: same three-step color chain; default returns NodeGrey → run_tests → confirm RED

### Implementation for US1

- [X] T017 [P] [US1] Implement Editor/Nodes/BaseNodeView.cs: abstract class extending Node; NodeData property; protected virtual HasColorOverride (default false); protected virtual ColorOverride (default Color.gray); public sealed Color ResolveColor() implementing the three-step chain (T010 + T011); abstract void OnBuildView(); load BaseNodeView.uss via AssetDatabase.LoadAssetAtPath in constructor
- [X] T018 [P] [US1] Implement Editor/Edges/BaseEdgeView.cs: abstract class extending Edge; EdgeData property; same HasColorOverride/ColorOverride/ResolveColor pattern as BaseNodeView; load BaseEdgeView.uss in constructor
- [X] T019 [US1] Implement Editor/Graph/BaseGraphView.cs core: abstract class extending GraphView; _graph field; _nodeViews Dictionary<string,BaseNodeView>; LoadGraph(BaseGraph) clears canvas, iterates graph.Nodes/graph.Edges, calls CreateNodeView/CreateEdgeView, positions nodes from NodeData.Position; abstract CreateNodeView(BaseNodeData)/CreateEdgeView(BaseEdgeData); load GraphCoreEditor.uss in constructor; graphViewChanged stub (no structural routing yet) → run_tests → confirm T015, T016 GREEN
- [X] T020 [US1] Implement Editor/Window/BaseGraphEditorWindow.cs: abstract class extending EditorWindow; protected BaseGraphView GraphView property; abstract CreateGraphView(); OnEnable instantiates view via CreateGraphView(), adds to rootVisualElement; protected LoadGraph(BaseGraph) delegates to GraphView.LoadGraph(); OnDisable removes GraphView from rootVisualElement
- [X] T021 [US1] Populate USS files with minimal functional styles: GraphCoreEditor.uss (canvas background color, grid lines via GraphView built-in class overrides); BaseNodeView.uss (title bar, content container padding, border radius); BaseEdgeView.uss (edge line color placeholder)

**Checkpoint**: US1 complete. Open window + load 3-node BaseGraph → all visible. Pan/zoom → asset unchanged.

---

## Phase 4: US3 - Save: One-Way Data Sync (Priority: P1)

**Goal**: Node positions sync to BaseGraph only on explicit save. Moving nodes does not dirty
the asset. SaveGraph() writes all positions in one atomic pass.

**Independent Test**: Move 3 nodes → EditorUtility.IsDirty(graph) = false. Call SaveGraph()
→ all node Position values updated in BaseGraph. Ctrl+S in window → same save invoked.

### Implementation for US3

- [ ] T022 [US3] Implement BaseGraphView.SaveGraph() in Editor/Graph/BaseGraphView.cs: iterate _nodeViews, write nodeView.GetPosition().position to nodeView.NodeData.Position; call EditorUtility.SetDirty(_graph); call AssetDatabase.SaveAssets(); guard with null check on _graph
- [ ] T023 [US3] Verify _isDirty is NOT set and SetDirty is NOT called in the graphViewChanged movedElements path; add a comment documenting that position sync is deferred to SaveGraph() per FR-003
- [ ] T024 [US3] Add toolbar to BaseGraphEditorWindow with a "Save" button (calls _graphView.SaveGraph()); register Ctrl+S keyboard shortcut on the root visual element

**Checkpoint**: US3 complete. Ctrl+S or Save button → positions written once. Node moves → no asset write.

---

## Phase 5: US2 - Authoring Hooks (Priority: P1)

**Goal**: Three protected virtual methods (OnNodeCreated, OnEdgeConnected, OnNodeDeleted)
fire when the canvas changes. Downstream libs override without replacing the class.

**Independent Test**: Subclass BaseGraphView, override all three hooks, record call count
and payload. Create a node → OnNodeCreated(nodeData) fires once with correct data. Draw
an edge → OnEdgeConnected(edgeData) fires. Delete a node → OnNodeDeleted(nodeData) fires
and all its attached edges are removed.

### Tests for US2

- [X] T025 [US2] Write failing tests in Tests/EditMode/Editor/BaseGraphViewHookTests.cs using a concrete test-double subclass: override OnNodeCreated/OnEdgeConnected/OnNodeDeleted to record invocations; add BaseNodeData/BaseEdgeData to graph programmatically and verify hooks fire; run_tests → confirm RED

### Implementation for US2

- [X] T026 [US2] Implement graphViewChanged createdElements routing in Editor/Graph/BaseGraphView.cs: for each added Node element → extract BaseNodeData from its BaseNodeView, call _graph.AddNode(nodeData), set _isDirty = true, call OnNodeCreated(nodeData); for each added Edge element → extract BaseEdgeData from its BaseEdgeView, call _graph.AddEdge(edgeData), set _isDirty = true, call OnEdgeConnected(edgeData)
- [X] T027 [US2] Implement graphViewChanged elementsToRemove routing: for each removed BaseNodeView → call _graph.RemoveNode(nodeData) (T005), remove all BaseEdgeViews whose EdgeData.FromNodeId or ToNodeId matches, call _graph.RemoveEdge for each, call OnNodeDeleted(nodeData); set _isDirty = true
- [X] T028 [US2] Expose OnNodeCreated, OnEdgeConnected, OnNodeDeleted as protected virtual void methods with empty base bodies in BaseGraphView.cs → run_tests → confirm T025 GREEN

**Checkpoint**: US2 complete. All three hooks fire with correct payloads. _isDirty = true after any structural change.

---

## Phase 6: US5 - Refuse Cyclic Edge Connections (Priority: P1)

**Goal**: CycleDetector.Check is called on EVERY OnEdgeConnected invocation. Cyclic connections
are refused visually with a [GraphCore]-prefixed error. Valid connections proceed normally.

**Independent Test**: Mutual SubGraph reference (A→B, B→A). Attempt completing edge → edge
not on canvas, Debug.LogError with "[GraphCore]" prefix. Also: valid connection → CycleDetector
called → HasCycle=false → edge accepted.

### Tests for US5

- [X] T029 [US5] Write failing tests in Tests/EditMode/Editor/CycleDetectorIntegrationTests.cs: create two BaseGraph assets; connect A→B via SubGraphNodeData; call CycleDetector.Check(A, B) → no cycle; call CycleDetector.Check(B, A) → cycle with path; verify BaseGraphView.OnEdgeConnected refuses edge when HasCycle=true → run_tests → confirm RED

### Implementation for US5

- [X] T030 [US5] Integrate CycleDetector into BaseGraphView.OnEdgeConnected in Editor/Graph/BaseGraphView.cs: after extracting BaseEdgeData, if target node is SubGraphNodeData with non-null TargetGraph → call CycleDetector.Check(_graph, targetGraph) → if result.HasCycle: remove the Edge from graphView elements, do NOT call _graph.AddEdge, do NOT set _isDirty, call Debug.LogError($"[GraphCore] Cycle detected: {string.Join(" → ", result.CyclePath)}")
- [X] T031 [US5] Ensure CycleDetector.Check is invoked even when the edge target is NOT a SubGraphNodeData — in that case Check(root, null) returns HasCycle=false immediately; verify this code path exists in BaseGraphView.OnEdgeConnected → run_tests → confirm T029 GREEN

**Checkpoint**: US5 complete. CycleDetector called on every connection. Cyclic edges refused + logged. Valid edges accepted.

---

## Phase 7: US4 - Copy/Paste with New GUIDs (Priority: P2)

**Goal**: Ctrl+C/V assigns new GUIDs to all pasted nodes and remaps intra-selection edges
to the new GUIDs. Original GUIDs never appear in the paste output.

**Independent Test**: Copy 2 connected nodes, paste → 2 new GUIDs ≠ originals; pasted edge
references new GUIDs. Paste again from same clipboard → 2 more GUIDs distinct from first
paste. No pasted GUID matches any existing node GUID.

### Tests for US4

- [X] T032 [P] [US4] Extend Tests/EditMode/Editor/CopyPasteGuidTests.cs (from T009) to cover GraphClipboardData serialization/deserialization round-trip and the full UnserializeAndPaste GUID reassignment logic using a test helper method extracted from BaseGraphView.CopyPaste.cs → run_tests → confirm RED on new assertions

### Implementation for US4

- [X] T033 [US4] Create Editor/Graph/BaseGraphView.CopyPaste.cs (partial class): override SerializeGraphElements(IEnumerable<GraphElement>) → filter for BaseNodeView and BaseEdgeView; serialize selected nodes to GraphClipboardData.Nodes (JsonUtility.ToJson per node); include only edges where both endpoints are selected; serialize to GraphClipboardData.Edges; return JsonUtility.ToJson(clipboardData)
- [X] T034 [US4] Implement UnserializeAndPaste(string operationName, string data) in BaseGraphView.CopyPaste.cs: deserialize GraphClipboardData; for each node: Guid.NewGuid().ToString("D") → build oldIdToNewId map; remap each edge's FromNodeId and ToNodeId via oldIdToNewId; assign new Guid to each edge; call CreateNodeView/CreateEdgeView; call _graph.AddNode/_graph.AddEdge; call OnNodeCreated/OnEdgeConnected; offset pasted nodes by a fixed delta to avoid overlap → run_tests → confirm T009, T032 GREEN

**Checkpoint**: US4 complete. Paste always produces non-overlapping fresh GUIDs. Two pastes from same clipboard → distinct GUID sets.

---

## Phase 8: US6 - Color Resolution Validation (Priority: P3)

**Goal**: The three-step color resolution chain works end-to-end for both BaseNodeView and
BaseEdgeView. Override → NodeTypeColorRegistry → GraphCoreDefaults.NodeGrey.

**Independent Test**: Create node view with HasColorOverride=true → override color used.
HasColorOverride=false + NodeTypeColorRegistry.Register for node's type → registry color used.
No override + no registry → NodeGrey. Same three assertions for BaseEdgeView.

### Tests for US6

- [X] T035 [P] [US6] Extend Tests/EditMode/Editor/BaseNodeViewColorTests.cs: add test that calls NodeTypeColorRegistry.Register("test/node", Color.red); creates a test-double BaseNodeView with NodeData.NodeType = "test/node"; verifies ResolveColor() returns Color.red; teardown calls NodeTypeColorRegistry.Clear() → run_tests → confirm RED
- [X] T036 [P] [US6] Extend Tests/EditMode/Editor/BaseEdgeViewColorTests.cs: same three-step chain integration tests; verify NodeGrey is returned when no override and type not registered; register a type color and verify it is returned → run_tests → confirm RED

### Implementation for US6

- [X] T037 [P] [US6] Review Editor/Nodes/BaseNodeView.cs ResolveColor() — confirm it calls NodeTypeColorRegistry.TryGet(NodeData.NodeType, out var c) correctly; if NodeData is null guard returns NodeGrey; adjust if any gap → run_tests → confirm T008, T035 GREEN
- [X] T038 [P] [US6] Review Editor/Edges/BaseEdgeView.cs ResolveColor() — confirm same three-step chain; EdgeData.HasColorOverride check uses EdgeData.EdgeColor (not NodeData.NodeColor); adjust if any gap → run_tests → confirm T016, T036 GREEN

**Checkpoint**: US6 complete. Color resolution chain verified end-to-end for both view types.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: XML documentation, USS refinement, constitution compliance verification.

- [X] T039 [P] Add XML <summary> documentation to all public API members: BaseGraphView, BaseNodeView, BaseEdgeView, BaseGraphEditorWindow, CycleDetector, CycleDetectionResult, NodeTypeColorRegistry, GraphCoreDefaults (per constitution Development Standards)
- [X] T040 [P] Grep Editor/ for inline C# style assignments: search for ".style." in all .cs files; zero hits required (FR-006); fix any found by moving to USS
- [X] T041 [P] Grep Editor/ for prohibited dependencies: "MonoBehaviour", "MonoScript", "UnityEvent", "dialoguesystem", "gameflow", "questsystem" — zero hits required (constitution Principle II + VI)
- [X] T042 Refine USS files with complete visual polish: GraphCoreEditor.uss (minimap position, toolbar area, grid color); BaseNodeView.uss (selection highlight, hover state, port dot styles); BaseEdgeView.uss (selected edge highlight color)
- [X] T043 Validate quickstart.md: create a minimal test lib subclass implementing all three abstract methods; open a window with a stub BaseGraph; confirm compiles and opens without errors; confirm Save button visible

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1/Phase 3**: Depends on Phase 2 — MVP entry point
- **US3/Phase 4**: Depends on US1 (canvas must exist)
- **US2/Phase 5**: Depends on US1 (graphViewChanged interception on existing canvas)
- **US5/Phase 6**: Depends on US2 (CycleDetector integrates into OnEdgeConnected hook)
- **US4/Phase 7**: Depends on US1 (canvas + CreateNodeView/CreateEdgeView must exist)
- **US6/Phase 8**: Depends on US1 (BaseNodeView/BaseEdgeView must exist)
- **Polish (Phase 9)**: Depends on all user story phases

### User Story Dependencies

| Story | Blocked by | Can parallelize with |
|-------|-----------|---------------------|
| US1 — canvas | Phase 2 | — |
| US3 — save | US1 | US2 first tasks |
| US2 — hooks | US1 | US3 |
| US5 — cycle detect | US2 (OnEdgeConnected hook exists) | US4, US6 |
| US4 — copy/paste | US1 (canvas + factories exist) | US5, US6 |
| US6 — colors | US1 (BaseNodeView/BaseEdgeView exist) | US4, US5 |

### Within Each Phase

- Test tasks must be RED before their paired implementation tasks begin
- For each story: tests → implementation → run_tests confirm GREEN
- Models/utilities before consuming views
- BaseNodeView + BaseEdgeView before BaseGraphView (view depends on them)

### Parallel Opportunities

```
Phase 1:  T002, T003 in parallel after T001
Phase 2:  T006, T007, T008, T009, T010, T012, T014 in parallel after T005
Phase 3:  T015 + T016 in parallel (tests); T017 + T018 in parallel (implementations)
Phase 8:  T035 + T036 in parallel (tests); T037 + T038 in parallel (implementations)
Phase 9:  T039, T040, T041 in parallel
```

---

## Parallel Example: US1

```
# Write tests in parallel (both reference non-yet-implemented classes — stubs needed first):
T015: Extend BaseNodeViewColorTests.cs (construction + USS load)
T016: Write BaseEdgeViewColorTests.cs (construction + color chain)

# Implement views in parallel (no shared file):
T017: Implement Editor/Nodes/BaseNodeView.cs
T018: Implement Editor/Edges/BaseEdgeView.cs

# Then sequentially (BaseGraphView depends on both views):
T019: Implement Editor/Graph/BaseGraphView.cs core
T020: Implement Editor/Window/BaseGraphEditorWindow.cs
T021: Populate USS files
```

---

## Implementation Strategy

### MVP (Phases 1–3: Setup + Foundational + US1)

1. Complete Phase 1: assembly + USS stubs (T001–T004)
2. Complete Phase 2: foundational logic proven by tests (T005–T014)
3. Complete Phase 3: US1 canvas (T015–T021)
4. **VALIDATE**: Open BaseGraphEditorWindow with a BaseGraph → nodes/edges visible, pan/zoom safe

### Incremental Delivery

1. Setup + Foundational → foundation proven
2. US1 → **MVP: any BaseGraph openable in the editor**
3. US3 → **round-trip: open, edit, save**
4. US2 → **libs react to authoring via hooks**
5. US5 → **safety: no cycle can be authored**
6. US4 → **productivity: copy/paste supported**
7. US6 → **UX: color theming chain validated**

---

## Notes

- [P] tasks operate on different files — safe to parallelize without merge conflicts
- [USn] labels map each task to its user story for full spec traceability
- TDD is mandatory: RED → GREEN per constitution Principle IV — never skip the RED confirmation
- T005 modifies the Runtime assembly (adds RemoveNode/RemoveEdge) — this is a MINOR API addition within the same feature branch, not a separate branch
- USS-only styling: zero `.style.*` C# property assignments permitted anywhere in the Editor assembly (T040 verifies this)
- `partial class` for BaseGraphView only — exactly two files: BaseGraphView.cs + BaseGraphView.CopyPaste.cs
- `CycleDetector.Check` is called on EVERY `OnEdgeConnected` regardless of node type — non-SubGraph edges produce HasCycle=false immediately (T031)
- `NodeTypeColorRegistry.Clear()` MUST be called in test teardown to prevent state leakage between test runs
