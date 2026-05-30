# Feature Specification: GraphTest Verification Package

**Feature Branch**: `005-graphtest-package`

**Created**: 2026-05-29

**Status**: Draft

**Input**: User description: "on va créer un com.faolline.graphTest sur le quel on va vérifier que tout fonctionne, avec la window de graph l'ajout des nodes leurs edges ect. le travail sur cette partie là nous améneras surment à apporter des modifications au graphcore"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open the Graph Editor Window (Priority: P1)

A developer opens the graph editor window provided by `com.faolline.graphTest` and sees a fully functional, empty canvas with a toolbar — no console errors, no missing UI.

**Why this priority**: The window is the entry point for all other verification. If it does not open cleanly, nothing else can be validated.

**Independent Test**: Can be fully tested by opening the window via the menu, observing the canvas and toolbar appear without errors, and closing it.

**Acceptance Scenarios**:

1. **Given** the `com.faolline.graphTest` package is installed, **When** the developer opens the graph window via the Unity menu, **Then** the editor window appears with an empty canvas and a toolbar containing at least a Save button — no errors in the console.
2. **Given** the window is already open, **When** Unity performs a domain reload (e.g., recompile), **Then** the window recovers and shows the previously loaded graph or an empty canvas — no unhandled exceptions.

---

### User Story 2 - Add Test Nodes to the Canvas (Priority: P1)

A developer right-clicks on the canvas and sees a context menu listing all available test node types. Selecting a type creates that node at the click position.

**Why this priority**: Node creation is the most fundamental authoring operation. Without it, edges and execution cannot be verified.

**Independent Test**: Can be fully tested by right-clicking the empty canvas, adding one node of each available type, and confirming each appears at the correct position without errors.

**Acceptance Scenarios**:

1. **Given** an open graph window with an empty canvas, **When** the developer right-clicks and selects a node type from the context menu, **Then** a node of that type appears at the click position.
2. **Given** the developer adds multiple nodes, **When** they save the graph and reopen the window, **Then** all added nodes are present at the same positions.
3. **Given** the developer selects a node and presses Delete, **When** the deletion is confirmed, **Then** the node is removed from the canvas and the graph data.

---

### User Story 3 - Connect Nodes with Edges (Priority: P1)

A developer drags from an output port on one node to an input port on another compatible node to create an edge. The edge is rendered on the canvas and persists with the graph.

**Why this priority**: Edges define the graph's execution flow. Without correct connection behavior, the entire data-model and runtime cannot be validated end to end.

**Independent Test**: Can be fully tested by creating two nodes, drawing an edge between them, saving, reloading, and confirming the edge still exists.

**Acceptance Scenarios**:

1. **Given** two nodes on the canvas, **When** the developer drags from the output port of node A to the input port of node B, **Then** an edge is drawn between them and is visible on the canvas.
2. **Given** a connected graph, **When** the developer saves and reopens the window, **Then** all edges are restored between the correct nodes.
3. **Given** two nodes where connection would create a cycle, **When** the developer attempts to draw the edge, **Then** the connection is rejected and the graph remains unchanged.
4. **Given** an edge on the canvas, **When** the developer selects and deletes the edge, **Then** the edge is removed and the graph data reflects the deletion.

---

### User Story 4 - Inspect and Edit Node Properties (Priority: P2)

A developer clicks on a node and sees its editable properties in the embedded inspector panel on the right side of the window. Changes made in the inspector are reflected in the graph data.

**Why this priority**: Verifying the inspector integration confirms the selection event pipeline and node data binding — components that will be needed by any real downstream library.

**Independent Test**: Can be fully tested by selecting a node with at least one text property, editing the value, saving, reloading, and confirming the edited value is preserved.

**Acceptance Scenarios**:

1. **Given** a node on the canvas, **When** the developer clicks it, **Then** the inspector panel on the right displays that node's properties.
2. **Given** a node is selected and the developer edits a text property in the inspector, **When** the graph is saved and reloaded, **Then** the edited value is preserved.
3. **Given** no node is selected, **When** the developer clicks empty canvas space, **Then** the inspector panel clears and shows no properties.

---

### User Story 5 - Execute a Test Graph (Priority: P2)

A developer triggers execution of a test graph from the editor window toolbar. The runtime traverses the graph from the Start node to the End node and logs each visited node to the console.

**Why this priority**: Runtime execution is the final integration checkpoint. It validates that the data model, execution engine, and editor all agree on graph structure.

**Independent Test**: Can be fully tested by building a minimal linear graph (Start → TestStatement → End), clicking Run in the toolbar, and observing that all three nodes are logged in order with no exceptions.

**Acceptance Scenarios**:

1. **Given** a valid linear graph (Start → content node → End), **When** the developer clicks the Run button in the toolbar, **Then** each node is visited in order and the run completes with a "Graph ended" message in the console.
2. **Given** a graph with a cyclic reference, **When** the developer clicks Run, **Then** the execution is aborted before any node runs and a clear cycle-detection error is logged.
3. **Given** an empty graph (no nodes), **When** the developer clicks Run, **Then** the attempt is rejected with a user-readable error indicating the graph has no start node.

---

### Edge Cases

- What if a graph asset is deleted from the Project window while the editor window is open? The window should gracefully clear its canvas and prompt the developer to load a different graph.
- What if two edges connect the same pair of ports? The second connection attempt should be rejected (duplicate edge guard).
- What if a node is dragged off the visible canvas area? Panning or auto-scroll should make it reachable; the node must not be permanently inaccessible.
- What if graphcore API changes break compilation of the test package? Build errors should surface clearly in the Unity console, not as silent runtime failures.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The package MUST provide a concrete graph asset type that can be created from the Unity Project window via the Create menu.
- **FR-002**: The package MUST provide an editor window that opens when the graph asset is opened (double-click or context menu).
- **FR-003**: The editor window MUST display a graph canvas and a toolbar containing at minimum a Save button and a Run button.
- **FR-004**: The package MUST define at least three distinct concrete node types: a Start-type node, at least one content node with at least one editable text property, and an End-type node.
- **FR-005**: Right-clicking the canvas MUST present a context menu listing all available test node types for creation.
- **FR-006**: Each created node MUST be positionable on the canvas, and its position MUST be serialized with the graph asset.
- **FR-007**: The developer MUST be able to draw an edge from the output port of one node to the input port of a compatible node.
- **FR-008**: All node data and edge data MUST survive a save/reload cycle (including Unity domain reload) with zero data loss.
- **FR-009**: The Run button in the toolbar MUST trigger in-editor execution of the loaded graph, logging each visited node and the final outcome to the Unity console.
- **FR-010**: Cycle detection MUST prevent execution of a graph that contains a cycle; a readable error MUST be logged before any node is visited.
- **FR-011**: The editor window MUST include an embedded inspector panel (split-pane layout) that displays the selected node's editable properties.
- **FR-012**: Changes made in the inspector panel MUST be reflected in the graph data and MUST persist after save/reload.
- **FR-013**: The package MUST include an assembly definition that references `com.faolline.graphcore` Runtime and Editor assemblies.
- **FR-014**: Any graphcore API gaps discovered during development of this package MUST be tracked as explicit sub-tasks; fixes go into graphcore and this package re-integrates them.

### Key Entities

- **TestGraph**: Concrete graph asset type that holds the full node and edge data for a test graph.
- **TestNodeTypes**: Set of at least three concrete node definitions — a Start node, a TestStatement node (with an editable text property), and an End node.
- **TestGraphEditorWindow**: Concrete editor window that hosts the canvas, toolbar (Save + Run), and inspector panel for the test package.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The graph editor window opens in under 2 seconds and shows an empty canvas with zero console errors.
- **SC-002**: Every available test node type can be created via the context menu without errors; all created nodes appear at the correct canvas position.
- **SC-003**: A graph containing at least 3 nodes and 2 edges can be saved and reloaded with 100% structural fidelity — same node count, same edge count, same positions.
- **SC-004**: Executing a valid linear graph (Start → TestStatement → End) completes in under 1 second and logs exactly three node-visit events plus one completion event, with zero console errors.
- **SC-005**: Selecting a node displays its properties in the inspector panel within one frame; editing a text property and saving preserves the new value after window close and reopen.
- **SC-006**: Attempting to execute a cyclic graph logs a cycle-detection error and zero node-visit events — the runtime never enters a node.

## Assumptions

- The package targets Unity 6000.x and depends on `com.faolline.graphcore` as a local package reference.
- The primary user is the library developer — this package is a verification and reference-implementation tool, not an end-user product.
- In-editor execution runs in EditMode (no Play Mode required); `BaseRunner` is already designed for headless execution.
- The test node types are purpose-built for verification; they do not represent any production domain (dialogue, quest, etc.).
- Discoveries during development that require graphcore changes are in scope for this feature but tracked as separate sub-tasks against the graphcore package.
- The embedded inspector panel relies on the `BaseNodeInspectorView` already introduced in the graphcore editor layer.
- Copy-paste, undo, and redo of graph operations are out of scope for this initial verification package.
