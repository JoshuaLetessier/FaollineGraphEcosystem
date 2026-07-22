# Faolline GraphSave — UnitySaveSystem Bridge

**Version**: 0.1.4 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphsave` ≥ 0.7.0, `com.faolline.savesystem.core` ≥ 1.0.0

Thin bridge that adapts a UnitySaveSystem backend (`ISaveSystem<T>`) to graphsave's
`IGraphSaveStore` contract. Install this only if you want to use UnitySaveSystem for persistence;
otherwise implement `IGraphSaveStore` yourself or serialize `GraphRunSnapshot` directly.

---

## Installation

See [`../INSTALL.md`](../INSTALL.md) for the full install guide.

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphsave.savesystem#master
```

You also need `com.faolline.savesystem.core` (the UnitySaveSystem core package) plus at least one
backend sub-package (e.g. `SSJson` for JSON file persistence, `SSPlayerPrefs` for PlayerPrefs).

---

## Usage

```csharp
using Faolline.GraphSave;
using Faolline.GraphSave.UnitySaveSystem;
using SaveSystem.SSJson;

// 1. Create the store by wrapping the UnitySaveSystem backend of your choice
var store = new SaveSystemGraphStore(
    new JsonSaveSystem<GraphRunSnapshot>());

// 2. Capture & save
var snapshot = GraphRunSnapshot.Capture(runner, context);
store.Save("slot0", snapshot);

// 3. Load & restore
if (store.Exists("slot0"))
{
    var loaded = store.Load("slot0");
    loaded.Restore(runner, graph, context);
}

// 4. Delete a slot
store.Delete("slot0");
```

---

## Architecture

```
com.faolline.graphsave.savesystem/
  Runtime/
    SaveSystemGraphStore.cs   ← IGraphSaveStore adapter wrapping ISaveSystem<GraphRunSnapshot>
```

One file, one class. All the heavy lifting is done by `com.faolline.graphsave` (the snapshot model)
and `com.faolline.savesystem.core` (the actual I/O).
