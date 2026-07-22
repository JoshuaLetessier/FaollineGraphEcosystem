# Faolline GraphGameFlow — Addressables Bridge

**Version**: 0.3.2 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphgameflow` ≥ 0.15.1, `com.unity.addressables` ≥ 2.2.2

Optional T3 adapter: an `ISceneLoader`/`ISceneUnloader` (from `com.faolline.graphgameflow`) backed by
`com.unity.addressables`, so `LoadSceneAction`/`UnloadSceneAction` can load a scene by **Addressable key**
instead of a **Build Settings** entry. Install this only if your project uses Addressables; graphgameflow
core stays dependency-free either way — this bridge exists precisely so Addressables is never forced on a
consumer who doesn't use it.

---

## Usage

```csharp
[SerializeField] private AddressablesSceneLoader _loader;   // component in your persistent bootstrap scene
[SerializeField] private GraphFlowDriver _flow;

void Awake() => _flow.SceneLoader = _loader;   // AddressablesSceneLoader is ISceneLoader/ISceneUnloader-compatible
```

Then on a `LoadSceneAction`/`UnloadSceneAction`, set **Scene Name** to the scene's **Addressable key**
(address, label, or GUID) — not necessarily its file name — and skip the Build Settings step entirely; a
`LoadSceneActionEditor` "not in Build Settings" warning is a false positive here.

`AddressablesSceneLoader` mirrors `AsyncSceneLoader`'s whole contract, so it is a drop-in swap wherever an
async loader is wanted:

- **Queued, never dropped**: concurrent load/unload requests join a FIFO queue drained serially.
  `IsLoading` / `PendingCount` expose the state.
- **Progress + lifecycle events**: `SceneLoadStarted`, `SceneLoadProgress(key, 0..1)`, `SceneLoadReady`,
  `SceneLoadCompleted`, `SceneUnloadStarted`, `SceneUnloadCompleted` — all keyed by the Addressable key.
- **Manual activation gate**: `AutoActivate = false` holds a loaded scene ready-but-inactive until
  `ActivateReadyScene()` is called (a fade-out, a "press to continue" beat); `MinimumDisplayDuration` avoids
  a one-frame flash.
- **Completion signals**: `LoadCompletedSignal` / `UnloadCompletedSignal` (`SignalDef`) + `SignalDriver`
  raise each completion into a `GraphFlowDriver` (key as string payload) — park the flow on an await-signal
  node right after the scene action and it resumes exactly when the operation lands. No manual event wiring.
- **Failure signals**: a failed load/unload (invalid key, missing content build…) never raises its
  completion signal, so a node awaiting only that would park forever. Set `LoadFailedSignal` /
  `UnloadFailedSignal` too, and add them as a SECOND `AwaitSignalNamesExtra` entry alongside the completion
  signal (graphcore's await-signal already resolves on any one of several — logical OR) — a failure resumes
  the flow instead of stalling it. Payload is `"{key}: {reason}"`, naming both what failed and why. Plain
  C# events `SceneLoadFailed`/`SceneUnloadFailed` (key, then reason) are also available. An invalid key can
  fail synchronously — the failure signal is deliberately delayed by one frame so it still delivers live to
  a node that just parked, rather than needing `BaseNodeData.ResumeIfSignalAlreadyRaised` to recover it (a
  useful backup for manually-advanced flows regardless). `SceneAwaitSetup.ConfigureLoadAwait` (in
  `com.faolline.graphgameflow`) sets up an await node for all of this in one call.
- **`StuckOperationWarningAfter`**: logs a warning (and raises `OperationTakingTooLong`) if a single
  load/unload has been in flight longer than a configurable threshold (default 15s; 0 disables it) — for the
  different failure shape signals can't help with, a request that never resolves to success OR failure at
  all. Diagnostic only; never alters the flow.
- **`PauseDriverWhileLoading`**: freezes the target driver's time (a parked timed wait holds) while the
  queue is busy, resuming it when the queue drains; never touches a pause the consumer set themselves.

The one difference from `SceneManager`-based unloading: Addressables needs the load's own operation handle
to unload, not just a name, so `UnloadScene` only works on a scene **this loader instance** loaded — an
unrecognised key logs a graceful `[GraphGameFlow]` error instead of throwing, exactly like every other
misuse in this ecosystem.

---

## Architecture

```
com.faolline.graphgameflow.addressables
│
└── Runtime/
    └── AddressablesSceneLoader.cs   ISceneLoader + ISceneUnloader over Addressables.LoadSceneAsync/UnloadSceneAsync
```

One file, one class — all the heavy lifting (the action model, the driver, the signal bridge) is done by
`com.faolline.graphgameflow`. This bridge only swaps the transport.

---

## Testing

- **EditMode**: argument guards and interface compliance — no Addressables initialisation needed.
- **PlayMode**: real `Addressables.LoadSceneAsync`/`UnloadSceneAsync` calls against the graphgameflow
  package's own committed cross-scene test scenes, registered as Addressable entries for the fixture's
  lifetime under the "Use Asset Database (fastest)" Play Mode script (no content build required). This is
  the standard way to exercise Addressables from the Editor; see `AddressablesSceneLoaderPlayModeTests` for
  the registration/cleanup fixture.

## Constraints

Same conventions as the rest of the ecosystem: `[GraphGameFlow]` log prefix on misuse, C# `Action<T>` events
(no `UnityEvent`), one class per file, XML docs.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
