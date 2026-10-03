# Quickstart: saving and restoring a run inside sub-graphs

Audience: a game using `GraphFlowDriver` with a chapter graph as root and zones as sub-graphs (Cryptique's setup).

## Capture (unchanged call)

```csharp
driver.OnNodeEntered += node =>
{
    if (!(node.IsCheckpoint || node is StartNodeData)) return;
    var snapshot = GraphRunSnapshot.Capture(driver.Runner, driver.Context);   // now records the whole stack
    saveStore.Save(slot, snapshot);
};
```

- `snapshot.Frames` lists every active level, root first: for `EverfrostFlow → ForêtFlow`, `[Everfrost @ SubGraph node, Forêt @ Start]`.
- `snapshot.GraphId` / `CurrentNodeId` still name the deepest level (Forêt, Start).
- `snapshot.RootGraphId` names the chapter graph (Everfrost). This is the only graph you need to load.
- Capture never throws. If a level runs on a *fresh* context (neither Inherit Parent Context nor Opens Scope) and the save cannot hold its values, it logs a `[GraphSave]` error naming the frame.

## Restore from the root graph you load yourself

```csharp
var snapshot = saveStore.Load(slot);
BaseGraph root = chapterRegistry.Get(snapshot.RootGraphId);   // or IGraphCatalog.Resolve(snapshot.RootGraphId, …)

if (!snapshot.TryValidate(root, out var error))
{
    // Content changed since the save (node deleted, sub-graph retargeted, wrong root…).
    // error.Reason, error.FrameIndex, error.DeepestValidFrameIndex, error.Message — choose your own fallback.
    return;
}

driver.Graph = root;
driver.Boot(snapshot);   // rebuilds Everfrost → Forêt; re-enters only Forêt's saved node
```

When Forêt reaches its End, the run returns into Everfrost after the SubGraph node and continues. The SubGraph node's own enter actions are not replayed.

If you skip `TryValidate` and the snapshot does not match, `Boot` logs a `[GraphGameFlow]` error, the driver stays inert and your context is not modified.

Without a driver: `snapshot.Restore(runner, root, context, registry)` does the same. On a mismatch it logs a `[GraphSave]` error and changes nothing.

## Limits

- Values written into an Opens Scope sub-graph's local layer are not saved: the layer restarts from its declared defaults. Exact when you save at that sub-graph's Start.
- A fresh-context sub-graph restarts from its declared defaults. Exact at its Start, reported as an error elsewhere.
- What enclosing graphs set up in the world before their SubGraph node (loaded scenes, spawned objects) is not rebuilt. Load it before `Boot`, as today.
- Waits restart: a node waiting for a signal or a timer is re-entered and waits again from the beginning.

## Validation (dev project, headless)

```text
Unity.exe -batchmode -projectPath H:/repos/Perso/LIB_Unity/FaollineGraphEcosystem -runTests -testPlatform EditMode
          -testFilter "Faolline.GraphCore.Tests.ExecutionPath;Faolline.GraphSave.Tests.GraphStackSnapshotTests;Faolline.GraphGameFlow.Tests.GraphFlowDriverSnapshotTests"
          -testResults <xml> -logFile <log>
```

Then run the full EditMode suite (no filter) and compare with the pre-change baseline.
