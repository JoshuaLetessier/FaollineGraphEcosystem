# Faolline GraphSave

**Version**: 0.11.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.44.0, `com.faolline.graphlogging` ≥ 0.2.0

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
  (`HasSignalBeenRaised`) — a transient wake-event has nothing to persist beyond "did it ever fire" — and, when
  captured from a runner, the whole **execution stack** (`Frames`: every level from the root graph down to the
  deepest sub-graph). It is the whole save model.
  ```csharp
  // capture (works at any depth, e.g. inside a zone sub-graph of a chapter graph)
  var snapshot = GraphRunSnapshot.Capture(runner, context);   // or Capture(context, graphId, nodeId): no stack

  // persist however you like — it is a plain JsonUtility-serializable object
  string json = JsonUtility.ToJson(snapshot);

  // restore from the ROOT graph — the only graph you need to load yourself
  var snapshot = JsonUtility.FromJson<GraphRunSnapshot>(json);
  BaseGraph root = myRootGraphs[snapshot.RootGraphId];
  snapshot.Restore(runner, root, context);   // rehydrates the context, rebuilds the stack, re-enters the saved node
  ```
- **`IGraphSaveStore`** — a neutral slot-based store contract (`Save`/`Load`/`Exists`/`Delete`/`GetAllKeys`/
  `DeleteAll`). Implement it against a file, PlayerPrefs, a cloud save, Steam Cloud, … — these are examples of
  what the contract allows, not backends this package ships (see **Backends** below for what's actually
  provided) — or skip it and (de)serialize the snapshot yourself.

## Backends

Three ways to store, from batteries-included to fully custom:

1. **`JsonFileGraphSaveStore`** — ships in this package, ready to use as-is: one JSON file per slot under
   `Application.persistentDataPath`, slot names validated against path-traversal/invalid characters, long/unicode
   slot names bounded to stay under Windows' `MAX_PATH`. `new JsonFileGraphSaveStore()` (or pass a custom
   sub-folder) is a complete `IGraphSaveStore`.
2. **`com.faolline.graphsave.savesystem`** (optional package) — bridges `com.faolline.savesystem.core`
   (UnitySaveSystem) rather than storing anything itself: it wraps whichever `ISaveSystem<T>` backend you've
   already set up there (`JsonSaveSystem`/`PlayerPrefsSaveSystem` today). Not a duplicate of
   `JsonFileGraphSaveStore` — reach for it when a project already standardizes on `savesystem.core` for its
   other save data and wants graph snapshots on the same pipeline (one backend to swap for encryption/cloud
   sync later) instead of running two separate save stacks. Its value scales with `savesystem.core`'s own
   backend catalog: limited to Json/PlayerPrefs today, but any cloud or encrypted backend that library gains,
   this bridge inherits for free. Add it only if you want that path; the core stays dependency-free.
3. **Your own** — implement `IGraphSaveStore` against encryption, cloud sync, a platform-specific backend, … or
   skip the interface entirely and (de)serialize `GraphRunSnapshot` yourself.

## Architecture

```
com.faolline.graphsave/
  Runtime/
    GraphRunSnapshot.cs      ← serializable save model (variables + collections + raised-signal history + node id + execution stack)
    IGraphSaveStore.cs       ← neutral slot-based store contract (Save / Load / Exists / Delete / GetAllKeys / DeleteAll)
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

// 5. Check it against the root graph (optional — Restore checks too), then restore: rehydrates the context
//    (variables + collections + raised-signal history), rebuilds the stack and re-enters the saved node
BaseGraph root = LoadMyRootGraph(snapshot.RootGraphId);
if (!snapshot.TryValidate(root, out var error))
{
    Debug.LogWarning(error.Message);   // content changed since the save — pick your own fallback
    return;
}
snapshot.Restore(runner, root, context);
```

**Saves inside sub-graphs (0.11.0).** `Capture(runner, context)` records the whole execution stack, root first:
for a chapter graph running a zone sub-graph, `Frames` = `[chapter @ the zone's SubGraph node, zone @ its
current node]`. `GraphId`/`CurrentNodeId` still name the deepest level; **`RootGraphId`** names the chapter. On
`Restore(runner, root, context)`, the graph you pass is that ROOT: the stack is rebuilt down the root's own
sub-graph nodes (no sub-graph is ever looked up by id), only the deepest saved node is re-entered (its enter
actions run, as before), the enclosing sub-graph nodes replay nothing, and when the zone ends the run continues
in the chapter. Pass the run's root context to `Capture` (e.g. `GraphFlowDriver.Context`). `Capture` never
throws, so it is safe to call from inside `OnNodeEntered` — a checkpoint or a sub-graph's Start node is a natural
save point.

**A save that no longer matches the content is refused, never guessed.** `TryValidate(root, out error)` is a pure
check: wrong root, a node deleted, a sub-graph node retargeted or switched to another context mode … `error`
gives the `Reason`, the level (`FrameIndex`) and the deepest level still valid (`DeepestValidFrameIndex`) so the
game can choose a fallback. `Restore` runs the same check first: on a mismatch it logs a `[GraphSave]` error and
changes nothing — no value applied, runner untouched.

**Limits.** Values written into an Opens Scope sub-graph's local layer are not saved (local values are transient
by design): that layer restarts from its declared defaults. A sub-graph with a fresh context (neither Inherit
Parent Context nor Opens Scope) restarts from its declared defaults too; `Capture` logs a `[GraphSave]` error
whenever that loses data (anywhere but on that sub-graph's entry node). World state that enclosing graphs set up
before their sub-graph node (loaded scenes, spawned objects) is the game's to restore, and a node waiting for a
signal or a timer waits again from the start.

**Snapshots without a stack** (`Capture(context, graphId, nodeId)`, or JSON written before 0.11.0) restore exactly
as before: the saved node is re-entered in whatever graph you pass, as a single level.

**`ApplyTo` merge vs. replace.** `Restore` applies the snapshot via `ApplyTo(context, replaceCollections: true)`,
which clears each captured collection first, making the snapshot authoritative. Calling `ApplyTo` directly with
the default `replaceCollections: false` instead MERGES — collection items are added rather than replacing — and
an item captured at quantity > 1 stacks again on every call, so applying the same snapshot twice with the
default doubles that quantity. Pass `replaceCollections: true` (or clear the collection yourself first) for an
idempotent re-apply.

## Layering

Sits above `com.faolline.graphcore` (it only uses `BaseContext`, `BaseRunner.GetExecutionPath`/`StartFrom`/
`StartFromPath` and `GraphRunPath`). Any host — a
gameflow driver, a dialogue, a future quest lib — can capture and restore a run with it. Nothing in the
ecosystem depends on graphsave; it is opt-in.
