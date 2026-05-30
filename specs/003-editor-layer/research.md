# Research: GraphCore Editor Layer

**Branch**: `003-editor-layer` | **Date**: 2026-05-28 | **Plan**: [plan.md](plan.md)

---

## R-01 · Unity GraphView API — Copy/Paste Mechanism

**Decision**: Override `GraphView.SerializeGraphElements` (copy) and
`GraphView.UnserializeAndPaste` (paste). Do not implement a custom clipboard format;
use Unity's JSON-serialized `GraphViewChange` pathway.

**Rationale**: These are the two callbacks Unity's `GraphView` provides explicitly for
copy/paste interception. Any other approach (direct clipboard manipulation, custom
`ISerializationCallbackReceiver`) bypasses the GraphView lifecycle and breaks undo.

**Alternatives considered**:
- Custom `ISerializationCallbackReceiver` on clipboard model — rejected: requires
  bypassing Unity's paste callback, loses GraphView undo integration.
- Implementing `OnPaste` as a menu command — rejected: Unity's built-in Ctrl+C/V only
  route through `SerializeGraphElements`/`UnserializeAndPaste`; menu commands would
  duplicate logic.

**GUID reassignment implementation**: In `UnserializeAndPaste`, iterate all deserialized
`BaseNodeData` objects, call `Guid.NewGuid().ToString("D")` for each, build a
`Dictionary<string, string> oldIdToNewId`, then remap all `BaseEdgeData.FromNodeId` and
`ToNodeId` references using the dictionary before adding to the canvas and graph.

---

## R-02 · USS File Loading in Editor Extensions

**Decision**: Store USS files under `Editor/Resources/GraphCore/`. Load via
`AssetDatabase.LoadAssetAtPath<StyleSheet>(path)` in `AddToClassList` setup, or via the
`styleSheets.Add(...)` call in each visual element's constructor.

**Rationale**: `Resources.Load<StyleSheet>` requires a `Resources` folder under the
package root or the Assets tree; `AssetDatabase.LoadAssetAtPath` works with explicit
relative paths inside a Unity package and does not pollute the runtime Resources budget
(editor-only assembly). `AssetDatabase` is the correct approach for editor-only assets in
a UPM package.

**Alternatives considered**:
- `Resources.Load<StyleSheet>` — rejected for a UPM package context: `Resources` in UPM
  packages must live under a specific folder structure and are less portable than
  `AssetDatabase` paths.
- Inline USS strings in C# — rejected: violates the "no inline styles" constraint.
- USS via `ThemeStyleSheet` (TSS) — rejected: TSS is intended for full application themes,
  not per-component overrides. Overkill for this scope.

**USS path convention**: `Editor/Resources/GraphCore/GraphCoreEditor.uss` (canvas base),
`Editor/Resources/GraphCore/BaseNodeView.uss`, `Editor/Resources/GraphCore/BaseEdgeView.uss`.
Subclasses in downstream libs add their own USS files; they do NOT modify graphcore USS.

---

## R-03 · ScriptableObject Asset Save Pattern

**Decision**: On explicit save, call `EditorUtility.SetDirty(graph)` then
`AssetDatabase.SaveAssets()`. The canvas-to-data sync pass (positions, edges) runs
immediately before `SetDirty`.

**Rationale**: This is the standard Unity workflow for editor-modified `ScriptableObject`
assets. `SetDirty` marks the object for inclusion in the next serialization; `SaveAssets`
flushes all dirty assets to disk atomically.

**Why not `Undo.RecordObject`**: `Undo.RecordObject` is appropriate when supporting
Editor undo, which is out of scope for this feature (Ctrl+Z undo of node edits is not
in the spec). Adding undo support is a future MINOR feature.

**Dirty-flag guard**: Before calling `SetDirty`, check `EditorUtility.IsDirty(graph)` or
maintain a local `_isDirty` bool in `BaseGraphView` that is set to `true` only when the
user makes a structural change (node add/delete/connect). Moving nodes sets `_isDirty`
but does not call `SetDirty`. This satisfies FR-003 (no write on move).

---

## R-04 · CycleDetector Algorithm

**Decision**: Iterative DFS using an explicit stack, with a `HashSet<string>` for visited
graph IDs and a second `HashSet<string>` for the current DFS recursion path.

**Rationale**: The standard DFS "white-grey-black" coloring is the textbook correct
algorithm for cycle detection in directed graphs. The iterative variant avoids stack
overflow on deep asset reference chains.

**Input**: `CycleDetector.Check(BaseGraph root, BaseGraph proposed)` takes the graph
that would contain the new SubGraph reference and the proposed target. It builds the
full dependency tree starting from `root`, treats `proposed` as if it were already
referenced, and checks if `proposed` can reach `root` (making it a cycle). The DFS
traverses `SubGraphNodeData.TargetGraph` references loaded via Unity's asset database.

**Cycle path reporting**: The DFS maintains a `List<string> path` of `GraphId` values
in the current recursion path. When a cycle is detected (a visited node is encountered on
the current path), the path list is returned as the `CycleDetectionResult.CyclePath`.

**Alternatives considered**:
- Recursive DFS — rejected: Unity projects with many nested graphs could cause stack
  overflow. Iterative is safer.
- Topological sort (Kahn's algorithm) — rejected: we only need to detect a cycle for
  a single proposed edge, not sort the whole graph. Kahn's would be slower and harder
  to produce a meaningful cycle path for the error message.
- Load all graph assets up front — rejected: too expensive; we traverse lazily via
  `SubGraphNodeData.TargetGraph` which Unity already has loaded in the asset database
  when the editor is open.

---

## R-05 · NodeTypeColorRegistry — Static vs. ScriptableObject

**Decision**: Static `Dictionary<string, Color>` in a `NodeTypeColorRegistry` class.
Libs register colors in their `[InitializeOnLoad]` static constructors.

**Rationale**: The simplest correct approach (YAGNI). Libs must be loaded in the Unity
Editor before their graphs can be opened, so `[InitializeOnLoad]` guarantees registration
happens before any `BaseNodeView` is constructed. A static registry needs no asset
authoring, no custom Inspector, and no load order dependency on ScriptableObjects.

**Alternatives considered**:
- `ScriptableObject` registry asset — rejected: requires manual setup per project,
  creates a writable asset that can be accidentally committed with lib-specific data.
- `AssetPostprocessor` — rejected: runs on import, not on editor load; registration
  timing is unreliable for opened graph windows.
- Per-node-type ScriptableObject palette — rejected: over-engineered for a simple
  color lookup; adds an extra asset authoring step for lib authors.

---

## R-06 · BaseGraphView Hooks — Virtual Methods vs. C# Events

**Decision**: Protected virtual methods (`OnNodeCreated`, `OnEdgeConnected`,
`OnNodeDeleted`) as the primary hook mechanism. Downstream libs subclass `BaseGraphView`
and override these methods.

**Rationale**: Consistent with Unity's own callback pattern in `GraphView` (e.g.,
`graphViewChanged`). Virtual methods in a class hierarchy are simpler than event
subscription for single-lib-per-window scenarios (each editor window hosts one lib's
view). No event subscription boilerplate is needed.

**Note on `graphViewChanged`**: `GraphView.graphViewChanged` is a delegate field
(not an event) invoked by Unity on every structural change. `BaseGraphView` intercepts
this delegate in its constructor, extracts `createdElements` / `movedElements` /
`elementsToRemove`, and routes to the three virtual methods. Downstream libs that
further override `graphViewChanged` must call `base.graphViewChanged`.

**Alternatives considered**:
- C# `event Action<T>` — rejected as primary hook: virtual method override is simpler
  for single-subclass scenarios and avoids subscription lifecycle management.
- Both events + virtual methods — rejected: YAGNI; adds API surface without a present
  requirement.

---

## R-07 · partial class for BaseGraphView

**Decision**: `BaseGraphView` uses `partial class` to split the file into:
`BaseGraphView.cs` (core lifecycle) and `BaseGraphView.CopyPaste.cs` (copy/paste logic).

**Rationale**: The constitution explicitly permits `partial` for `BaseGraphView` only
(Development Standards: "partial classes are permitted for BaseGraphView only"). The
copy/paste logic is self-contained and substantial enough to warrant its own file without
introducing a new class.

**Scope**: Exactly two partial files. No further splits.
