# com.faolline.starterGraph

**Version**: 0.5.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.38.0

**Internal verification package — not for distribution.**

A concrete, minimal subclass of `com.faolline.graphcore` used to exercise the full editor and runtime
surface (a reference "starter" graph implementation + sample assets). It validates that graphcore can
be specialized by a downstream lib.

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist (`com.faolline.graphcore/Editor/GraphEcosystemModules.json`) and nothing depends on it, so a
consumer project never receives it. Kept in the repo for development and CI only.

---

## Using starterGraph as a template for your own graph lib

This package is designed to be forked and renamed. Follow these steps:

1. **Copy the folder** — duplicate `com.faolline.starterGraph` and rename it to
   `com.faolline.yourlibname` (or your own reverse-DNS).

2. **Rename the package** — in `package.json`, change:
   - `name` → your package name
   - `displayName` → your display name
   - `version` → `0.1.0`

3. **Rename the namespace** — replace `Faolline.StarterGraph` with your namespace in all `.cs` files
   and in the `.asmdef` files (Runtime, Editor, and Tests/EditMode).

4. **Rename the asset types** — `StarterGraph.cs` is your graph ScriptableObject. Rename it and
   update its `[CreateAssetMenu]` path. Same for `StarterContext.cs` and `StarterContextKeys.cs`.

5. **Rename the editor window** — `StarterGraphEditorWindow.cs` registers the graph editor. Change
   the `[MenuItem]` path and the window title.

6. **Update assembly definitions** — rename all three `.asmdef` files and update their references:
   - `com.faolline.starterGraph.Runtime.asmdef` → your runtime asmdef
   - `com.faolline.starterGraph.Editor.asmdef` → your editor asmdef
   - `com.faolline.starterGraph.Tests.EditMode.asmdef` → your test asmdef

   The `Tests/EditMode/` folder itself is a decision point: keep and rename it as regression
   scaffold for your new package (it already covers the context/graph/editor surface you're about to
   modify), or delete it and start your own test assembly from scratch. Either is reasonable — pick
   based on how much of the starter behavior you're keeping.

7. **Register in the module selector** *(optional)* — add an entry in
   `com.faolline.graphcore/Editor/GraphEcosystemModules.json` if you want your lib to appear in
   **Window ▸ Faolline ▸ Graph Ecosystem Modules**.

---

## Architecture

```
com.faolline.starterGraph/
  Runtime/
    StarterGraph.cs             ← ScriptableObject graph asset (extends BaseGraph)
    StarterContext.cs           ← Custom context (extends BaseContext)
    StarterContextKeys.cs       ← Typed variable key constants (raw-string channel; see graphcore's VariableDef for the governed asset channel)
    Choices/
      StarterChoice.cs           ← Example choice implementation
    Nodes/
      StarterStatementNodeData.cs ← Example statement node data
  Editor/
    Window/
      StarterGraphEditorWindow.cs ← Graph editor window (extends BaseGraphEditorWindow)
    Graph/
      StarterGraphView.cs         ← GraphView surface for StarterGraph
    Edges/
      StarterEdgeView.cs          ← Edge view for the starter graph
    Inspector/
      StarterNodeInspectorView.cs ← Node inspector view for the starter graph
    Nodes/                       ← Node-view implementations (5 files: Choice, End, Start, StarterStatement, SubGraph)
    Samples/
      StarterSampleBuilder.cs     ← Menu-driven sample graph generator
  Samples/
    StarterSampleGraph.asset    ← Example graph asset for quick testing
  Tests/
    EditMode/                   ← NUnit test assembly, own asmdef (com.faolline.starterGraph.Tests.EditMode)
      Editor/                    ← Editor-surface tests (StarterEditorTests, StarterRobustnessTests, StarterWindowExecutionTests)
      Runtime/                   ← Runtime coverage (StarterContextContractTests, StarterRuntimeTests)
```
