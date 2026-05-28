# Feature Specification: GraphCore Editor Layer

**Feature Branch**: `003-editor-layer`

**Created**: 2026-05-28

**Status**: Draft

**Input**: User description: "Je veux construire la couche éditeur de com.faolline.graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open and Navigate a Graph in the Editor Window (Priority: P1)

A developer opens a `BaseGraphEditorWindow`, loads a `BaseGraph` asset, and sees all nodes
and edges rendered in the canvas. Nodes are displayed with their type-resolved color. The
developer can pan, zoom, and inspect nodes without any data being written back to the asset
until an explicit save action.

**Why this priority**: This is the entry point for all graph authoring. Without a functional
editor window and canvas, no other editor feature is reachable.

**Independent Test**: Open a `BaseGraphEditorWindow` with a `BaseGraph` containing three nodes
and two edges. Verify all nodes and edges are rendered. Verify the asset's serialized data is
unchanged after panning and zooming.

**Acceptance Scenarios**:

1. **Given** a `BaseGraph` asset with 3 nodes and 2 edges, **When** the editor window is
   opened, **Then** all 3 `BaseNodeView` instances and 2 `BaseEdgeView` instances appear on
   the canvas.
2. **Given** the editor window is open, **When** the developer pans or zooms,
   **Then** the underlying `BaseGraph` asset is not modified (dirty flag remains false).
3. **Given** the editor window is open, **When** a node is moved on the canvas,
   **Then** the `BaseGraph` asset is not modified until the developer explicitly saves.

---

### User Story 2 - Create, Connect, and Delete Nodes (Priority: P1)

A developer creates a new node via the canvas context menu, connects it to an existing node
by dragging an edge, and deletes a node. The `OnNodeCreated`, `OnEdgeConnected`, and
`OnNodeDeleted` hooks are invoked so that downstream libs can react without replacing the
editor class.

**Why this priority**: Node authoring is the core graph editing workflow. The hook mechanism
is the integration contract with ecosystem libs.

**Independent Test**: Subclass `BaseGraphView`, subscribe to `OnNodeCreated` and
`OnEdgeConnected`. Create a node and draw an edge. Assert both hooks fire with the correct
node/edge data. Delete the node and assert `OnNodeDeleted` fires.

**Acceptance Scenarios**:

1. **Given** a `BaseGraphView` subclass with an `OnNodeCreated` subscriber,
   **When** a new node is added to the canvas, **Then** `OnNodeCreated` fires with the new
   node's data reference.
2. **Given** two nodes on the canvas, **When** the developer connects them with an edge,
   **Then** `OnEdgeConnected` fires with the source and destination port data.
3. **Given** a node on the canvas, **When** the developer deletes it,
   **Then** `OnNodeDeleted` fires with the deleted node's data reference, and all edges
   attached to that node are also removed from the canvas.
4. **Given** a `BaseGraphView` subclass that overrides no methods, **When** any of the three
   hooks fire, **Then** the base implementation completes without error (hooks are additive,
   not replacing).

---

### User Story 3 - Save Graph: One-Way Data Sync (Priority: P1)

A developer moves nodes around the canvas and edits properties. Nothing is written to the
asset during these interactions. When the developer triggers an explicit save (e.g., Ctrl+S
or a Save button), the visual state is serialized into the `BaseGraph` asset in one pass.

**Why this priority**: The one-way, save-only sync model is a deliberate constraint that
prevents partial/corrupt writes and keeps editor performance high for large graphs.

**Independent Test**: Move 3 nodes to new positions, verify no changes on the asset, trigger
save, verify node positions in the asset match the canvas positions.

**Acceptance Scenarios**:

1. **Given** nodes moved to new positions, **When** save has not been triggered,
   **Then** `BaseGraph.Nodes` position values are unchanged.
2. **Given** nodes at new positions, **When** save is triggered, **Then** all node positions
   in `BaseGraph.Nodes` are updated in a single write to match the canvas layout.
3. **Given** a save in progress, **When** the write completes, **Then** no partial/intermediate
   state is persisted — the asset is either fully updated or unchanged.

---

### User Story 4 - Copy/Paste Nodes with New GUIDs (Priority: P2)

A developer selects one or more nodes, copies them, and pastes. The pasted nodes receive new
GUIDs; all edges between copied nodes are remapped to the new GUIDs. The original GUIDs are
never reused in the paste output, even if the paste target is a different graph.

**Why this priority**: Copy/paste is a standard authoring convenience. The GUID reassignment
rule is a hard constraint that prevents data corruption when graph assets are shared or
referenced.

**Independent Test**: Copy two connected nodes, paste once. Verify the pasted nodes have
GUIDs different from the originals. Verify the pasted edge references the new GUIDs, not the
originals. Paste a second time and verify each paste produces a distinct set of new GUIDs.

**Acceptance Scenarios**:

1. **Given** two connected nodes are copied and pasted, **When** paste completes,
   **Then** the two pasted nodes have GUIDs that differ from the original nodes.
2. **Given** an edge between two copied nodes, **When** paste completes, **Then** the pasted
   edge references the pasted nodes' new GUIDs, not the original GUIDs.
3. **Given** a paste operation, **When** performed twice from the same clipboard,
   **Then** each paste produces a distinct, non-overlapping set of GUIDs.
4. **Given** any paste operation, **When** inspecting the pasted node data,
   **Then** no pasted node GUID matches any existing node GUID in the graph.

---

### User Story 5 - Refuse Cyclic Edge Connections (Priority: P1)

A developer attempts to draw an edge that would create a dependency cycle between `BaseGraph`
assets (e.g., Graph A references Graph B via a SubGraph node, and Graph B already references
Graph A). The `CycleDetector` runs a DFS on every `OnEdgeConnected` event without exception
and refuses the connection visually, showing an error message.

**Why this priority**: Cycle prevention is a non-negotiable safety requirement per the
project constitution. It must run on every edge connection attempt, including when the
connection is valid, to ensure no cycle slips through.

**Independent Test**: Build two graph assets with a mutual SubGraph reference. Attempt to
draw the completing edge. Verify the edge is not added to the canvas and an error message
is shown. Verify that `CycleDetector` is also called on a valid (non-cyclic) connection and
completes without refusing.

**Acceptance Scenarios**:

1. **Given** graph A → B dependency exists, **When** the developer tries to add an edge that
   creates B → A, **Then** the edge is not added and a visible error message is displayed.
2. **Given** a valid (non-cyclic) edge connection, **When** `OnEdgeConnected` fires,
   **Then** `CycleDetector` runs, finds no cycle, and the edge is accepted normally.
3. **Given** any edge connection attempt, **When** `OnEdgeConnected` fires,
   **Then** `CycleDetector` is invoked — there is no code path that skips it.
4. **Given** a cycle is detected, **When** the error is shown, **Then** the message
   identifies the dependency path that would form the cycle.

---

### User Story 6 - Node and Edge Color Resolution (Priority: P3)

A developer registers a color override for a custom node type in a downstream lib. When that
node type is rendered, it displays the lib-provided color. If no override is registered, the
node falls back to the type-level color defined by the lib, and further to the graphcore
default grey if neither is provided. Edge color follows the same resolution chain.

**Why this priority**: Color theming is a UX enhancement that makes large graphs easier to
read. It does not block core authoring functionality.

**Independent Test**: Create a `BaseNodeView` subclass with `HasColorOverride = true` and a
custom color. Verify the node renders with that color. Set `HasColorOverride = false` and
verify it falls back to the lib type color, then remove the type color and verify the graphcore
grey fallback.

**Acceptance Scenarios**:

1. **Given** a `BaseNodeView` where `HasColorOverride` returns `true`, **When** the node is
   rendered, **Then** the node background uses the override color.
2. **Given** a `BaseNodeView` where `HasColorOverride` returns `false` but a lib type color
   is registered, **When** rendered, **Then** the node uses the lib type color.
3. **Given** a `BaseNodeView` with no override and no lib type color, **When** rendered,
   **Then** the node uses the graphcore default grey.
4. **Given** a `BaseEdgeView`, **When** rendered, **Then** the same three-step color
   resolution chain applies as for `BaseNodeView`.

---

### Edge Cases

- What happens when a `BaseGraph` asset is null or missing when the editor window opens?
  The window should show an empty/disabled canvas with a descriptive message; no exception.
- How does the system handle saving when no changes have been made?
  Save is a no-op (no asset dirty flag set, no serialization pass triggered).
- What happens when `OnEdgeConnected` fires for an edge between nodes in the same graph
  (not a cross-asset SubGraph reference)? `CycleDetector` runs, finds no cross-asset cycle,
  and accepts the connection.
- What happens when copy/paste is triggered with nothing selected?
  Paste is a no-op; no nodes or edges are created.
- What if a node type has no registered color at any level?
  The graphcore default grey is always the final fallback — color resolution never fails.
- What if two different libs register a type color for the same node type string?
  The last registration wins (same replacement semantics as `NodeExecutorRegistry`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `BaseGraphView` MUST extend Unity's `GraphView` class and be usable as a
  drop-in canvas within any `BaseGraphEditorWindow`.
- **FR-002**: `BaseGraphView` MUST expose three overridable hooks as protected virtual methods
  or C# events: `OnNodeCreated(BaseNodeData)`, `OnEdgeConnected(BaseEdgeData)`,
  and `OnNodeDeleted(BaseNodeData)`. Downstream libs subscribe without replacing the class.
- **FR-003**: Visual-to-data synchronization MUST be one-way and triggered only on explicit
  save. No `BaseGraph` asset field MUST be written during node movement, zoom, or pan.
- **FR-004**: Copy/paste MUST assign a new GUID to every pasted node. Reusing the original
  GUID in the paste output is forbidden.
- **FR-005**: Copy/paste MUST remap all edges between copied nodes to reference the pasted
  nodes' new GUIDs. No pasted edge MUST reference an original (pre-paste) GUID.
- **FR-006**: All visual styling MUST be defined in USS files. No inline style assignments
  in C# editor code are permitted.
- **FR-007**: `BaseNodeView` MUST implement a three-step color resolution:
  1. If `HasColorOverride` returns `true`, use the override color.
  2. Else if a lib type color is registered for this node type, use it.
  3. Else use graphcore default grey (`#808080`).
- **FR-008**: `BaseEdgeView` MUST implement the same three-step color resolution as
  `BaseNodeView`.
- **FR-009**: `BaseGraphEditorWindow` MUST extend Unity's `EditorWindow` class and host a
  `BaseGraphView` as its primary canvas element.
- **FR-010**: `CycleDetector` MUST perform a DFS over the `BaseGraph` asset dependency graph
  (following `SubGraphNodeData.TargetGraph` references) to detect cycles.
- **FR-011**: `CycleDetector` MUST be invoked on every `OnEdgeConnected` event without
  exception — there is no code path that bypasses it.
- **FR-012**: When `CycleDetector` detects a cycle, it MUST refuse the connection visually
  (the edge MUST NOT be added to the canvas) and display an error message identifying the
  cyclic dependency path.
- **FR-013**: `CycleDetector` MUST NOT modify any `BaseGraph` asset; it is read-only during
  its DFS traversal.
- **FR-014**: All editor classes MUST live in an Editor assembly (`com.faolline.graphcore.Editor.asmdef`)
  separate from the Runtime assembly. No Runtime class MUST depend on any Editor class.

### Key Entities

- **BaseGraphView**: Canvas component extending `GraphView`; owns node/edge rendering and
  the three authoring hooks.
- **BaseNodeView**: Visual representation of a `BaseNodeData`; owns the three-step color
  resolution and delegates property rendering to subclasses.
- **BaseEdgeView**: Visual representation of an edge between two ports; owns the same
  three-step color resolution as `BaseNodeView`.
- **BaseGraphEditorWindow**: `EditorWindow` host that opens a `BaseGraphView` canvas for a
  given `BaseGraph` asset.
- **CycleDetector**: Stateless utility that performs a DFS over `BaseGraph` asset
  dependencies and returns a cycle report (detected/not detected, cycle path).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can open a `BaseGraphEditorWindow`, add three nodes, connect them,
  and save — all without touching any Runtime assembly file.
- **SC-002**: Moving 100 nodes on the canvas produces zero writes to the `BaseGraph` asset
  before save is triggered (verifiable via Unity's asset dirty-flag API).
- **SC-003**: Copy/pasting a selection of 10 connected nodes produces 10 new unique GUIDs
  and 0 GUID collisions with pre-existing nodes in 100% of test runs.
- **SC-004**: `CycleDetector` correctly identifies all cycles in 100% of test scenarios,
  including indirect cycles (A → B → C → A), and refuses the completing edge in every case.
- **SC-005**: `CycleDetector` is invoked on every edge connection attempt — validated by a
  test that counts invocations across 50 valid and 50 invalid connection attempts.
- **SC-006**: Zero inline C# style assignments exist in the editor assembly — verified by
  static analysis (grep for `.style.` assignments outside USS context).
- **SC-007**: A downstream lib can subscribe to `OnNodeCreated`, `OnEdgeConnected`, and
  `OnNodeDeleted` without subclassing `BaseGraphView` with a replacement, validated by an
  integration test with a stub subscriber.
- **SC-008**: The editor assembly compiles with zero errors and zero warnings in a fresh Unity
  project alongside the Runtime assembly, with no ecosystem lib installed.

## Assumptions

- The editor layer targets Unity 6000.x (Unity 6), which includes the `GraphView` experimental
  API under `UnityEditor.Experimental.GraphView`. This API is assumed stable enough for
  internal graphcore use.
- `BaseGraphView` hooks (`OnNodeCreated`, `OnEdgeConnected`, `OnNodeDeleted`) are implemented
  as protected virtual methods. Libs subclass `BaseGraphView` to override them; event-based
  subscription is an alternative only if the lib does not subclass.
- The `CycleDetector` operates on `BaseGraph` asset references loaded in the Unity asset
  database — it does not need to open or parse asset files directly.
- "Explicit save" means Ctrl+S within the focused `BaseGraphEditorWindow`, or a Save button
  in the window toolbar. Auto-save on window close is out of scope.
- `HasColorOverride` is a virtual property on `BaseNodeView` returning `false` by default;
  subclasses return `true` and provide the override color via a companion virtual property.
- The lib type color registry is a static dictionary keyed by node type string, accessible to
  both `BaseNodeView` and `BaseEdgeView`. Its management API is defined by the lib, not by
  graphcore.
- Graphcore default grey for the color fallback is `#808080`.
- The editor assembly (`com.faolline.graphcore.Editor.asmdef`) does not yet exist; this
  feature creates it alongside the Runtime assembly from `001-data-layer`.
- Copy/paste targets the same graph window. Cross-window paste (copying from graph A into
  graph B) is out of scope for this feature.
- `CycleDetector` DFS depth is bounded by the number of `BaseGraph` assets in the project;
  no explicit depth limit is enforced (Unity's asset database size is the practical bound).
