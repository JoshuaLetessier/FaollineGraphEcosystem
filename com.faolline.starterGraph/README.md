# com.faolline.starterGraph

**Version**: 0.3.1 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.18.0

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
   and in the `.asmdef` files (both Runtime and Editor).

4. **Rename the asset types** — `StarterGraph.cs` is your graph ScriptableObject. Rename it and
   update its `[CreateAssetMenu]` path. Same for `StarterContext.cs` and `StarterContextKeys.cs`.

5. **Rename the editor window** — `StarterGraphEditorWindow.cs` registers the graph editor. Change
   the `[MenuItem]` path and the window title.

6. **Update assembly definitions** — rename both `.asmdef` files and update their references:
   - `com.faolline.starterGraph.Runtime.asmdef` → your runtime asmdef
   - `com.faolline.starterGraph.Editor.asmdef` → your editor asmdef

7. **Register in the module selector** *(optional)* — add an entry in
   `com.faolline.graphcore/Editor/GraphEcosystemModules.json` if you want your lib to appear in
   **Window ▸ Faolline ▸ Graph Ecosystem Modules**.

---

## Architecture

```
com.faolline.starterGraph/
  Runtime/
    StarterGraph.cs           ← ScriptableObject graph asset (extends BaseGraph)
    StarterContext.cs          ← Custom context (extends BaseContext)
    StarterContextKeys.cs      ← Typed parameter key constants
  Editor/
    StarterGraphEditorWindow.cs ← Graph editor window (extends BaseGraphEditorWindow)
  Samples/
    StarterSampleGraph.asset   ← Example graph asset for quick testing
```
