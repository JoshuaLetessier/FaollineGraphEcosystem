# Faolline GraphSave

**Version**: 0.7.1 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.35.0

Optional persistence layer for the Faolline graph ecosystem. It is to saving what `graphlocalization` is to
text: a neutral model plus a backend seam, so you plug in whatever store you want.

---

## Installation

See [`../INSTALL.md`](../INSTALL.md) for the full install guide (module selector or manual git URL).

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphsave#master
```

## What it gives you

- **`GraphRunSnapshot`** — a serializable photo of a running graph: the `BaseContext`'s typed **variables**
  (its `GetAllVariables()` snapshot — the Variable primitive, see graphcore's README) and named **collections**,
  plus the current node id. **Signals are deliberately not captured** here except the durable raised-history
  (`HasSignalBeenRaised`) — a transient wake-event has nothing to persist beyond "did it ever fire". It is the
  whole save model.
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

## Architecture

```
com.faolline.graphsave/
  Runtime/
    GraphRunSnapshot.cs      ← serializable save model (variables + collections + raised-signal history + node id)
    IGraphSaveStore.cs       ← neutral slot-based store contract (Save / Load / Exists / Delete)
```

## Full round-trip example

```csharp
// 1. Capture the current state
var snapshot = GraphRunSnapshot.Capture(runner, context);

// 2. Serialize to JSON (or any format — it's a plain C# object)
string json = JsonUtility.ToJson(snapshot);

// 3. Write to disk / PlayerPrefs / cloud — your choice
System.IO.File.WriteAllText(savePath, json);

// ─── later, on load ───

// 4. Deserialize
string json = System.IO.File.ReadAllText(savePath);
var snapshot = JsonUtility.FromJson<GraphRunSnapshot>(json);

// 5. Restore: rehydrates the context (variables + collections + raised-signal history) and re-enters the saved node
snapshot.Restore(runner, graph, context);
```

## Layering

Sits above `com.faolline.graphcore` (it only reads `BaseContext` + `BaseRunner.StartFrom`). Any host — a
gameflow driver, a dialogue, a future quest lib — can capture and restore a run with it. Nothing in the
ecosystem depends on graphsave; it is opt-in.
