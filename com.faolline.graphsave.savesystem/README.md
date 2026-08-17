# Faolline GraphSave — UnitySaveSystem Bridge

**Version**: 0.2.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphsave` ≥ 0.10.0, `com.faolline.savesystem.core` ≥ 1.0.0, `com.faolline.graphlogging` ≥ 0.1.1

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

// 5. List every slot the backend holds, or wipe them all
foreach (var key in store.GetAllKeys()) { /* ... */ }
store.DeleteAll();
```

---

## Failure handling

Every `SaveSystemGraphStore` method wraps its backend call in a try/catch: a throwing backend never
propagates past the bridge. `Save`/`Delete`/`DeleteAll` log a `[GraphSave]` error/warning and no-op;
`Load`/`Exists`/`GetAllKeys` log and degrade to `null`/`false`/empty instead — the same "absent" contract
`IGraphSaveStore.Load` already documents for a missing slot. A consumer expecting exceptions to propagate
from a corrupt save file or a transient I/O failure needs to know they won't; check the return value instead.

`Exists()` also cross-checks against the backend's own `Load()`, not just its raw presence check: some
backends (e.g. `JsonSaveSystem`) validate integrity (a checksum) inside `Load()` but not inside `Exists()`,
so a corrupted-but-present file could otherwise report `true` from `Exists()` while `Load()` correctly (and
gracefully) returns `null` for that same slot. This bridge's `Exists()` never reports `true` for a slot
`Load()` would refuse, so the common `if (store.Exists(slot)) store.Load(slot)` pattern never gets `null`.

---

## Architecture

```
com.faolline.graphsave.savesystem/
  Runtime/
    SaveSystemGraphStore.cs   ← IGraphSaveStore adapter wrapping ISaveSystem<GraphRunSnapshot>
```

One file, one class. All the heavy lifting is done by `com.faolline.graphsave` (the snapshot model)
and `com.faolline.savesystem.core` (the actual I/O). Logging goes through the shared
`com.faolline.graphlogging` facade (`Logging.*`, `"GraphSave"` category), toggleable from
**Faolline ▸ Diagnostics ▸ Log Settings**.
