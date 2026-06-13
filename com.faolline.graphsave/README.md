# Faolline GraphSave

Optional persistence layer for the Faolline graph ecosystem. It is to saving what `graphlocalization` is to
text: a neutral model plus a backend seam, so you plug in whatever store you want.

## What it gives you

- **`GraphRunSnapshot`** — a serializable photo of a running graph: the `BaseContext`'s typed parameters and
  named collections, plus the current node id. It is the whole save model.
  ```csharp
  // capture
  var snapshot = GraphRunSnapshot.Capture(runner, context);   // or Capture(context, graphId, nodeId)

  // persist however you like — it is a plain JsonUtility-serializable object
  string json = JsonUtility.ToJson(snapshot);

  // restore
  var snapshot = JsonUtility.FromJson<GraphRunSnapshot>(json);
  snapshot.Restore(runner, graph, context);   // rehydrates the context + re-enters the saved node
  ```
- **`IGraphSaveStore`** — a neutral slot-based store contract (`Save`/`Load`/`Exists`/`Delete`). Implement it
  against a file, PlayerPrefs, a cloud save, Steam, … — or skip it and (de)serialize the snapshot yourself.

## Backends

graphsave ships **no** backend — it does not reinvent persistence. Two ways to store:

1. **Your own** — implement `IGraphSaveStore`, or just serialize `GraphRunSnapshot` directly.
2. **`com.faolline.graphsave.savesystem`** (optional package) — bridges `com.faolline.savesystem.core`
   (UnitySaveSystem), whose `JsonSaveSystem`/`PlayerPrefsSaveSystem` backends do the actual writing. Add it only
   if you want that path; the core stays dependency-free.

## Layering

Sits above `com.faolline.graphcore` (it only reads `BaseContext` + `BaseRunner.StartFrom`). Any host — a
gameflow driver, a dialogue, a future quest lib — can capture and restore a run with it. Nothing in the
ecosystem depends on graphsave; it is opt-in.
