# Phase 1 — Data Model: gameflow driver cross-scene hardening

All changes are **additive** to `GraphFlowDriver` (`Faolline.GraphGameFlow`). graphcore/graphstandard
unchanged; existing members unchanged.

## GraphFlowDriver — added members

| Member | Kind | Description |
|--------|------|-------------|
| `_persistAcrossScenes` | `[SerializeField] bool` (default `false`) | Opt-in: keep this driver alive across scene loads. |
| `PersistAcrossScenes` | `bool` property (get/set) | Inspector/code access to the flag (read at `Awake`). |
| `_bootOnStart` | `[SerializeField] bool` (default `true`) | When false, the Unity `Start` hook does NOT auto-boot. |
| `BootOnStart` | `bool` property (get/set) | Access to the flag. |
| `Active` | `static GraphFlowDriver` (get; private set) | The current persistent driver, or null. Set in `Awake` when persistent. |
| `OnWaitingForTime` | `event Action<BaseNodeData, float>` | Re-exposed from the runner: flow entered a timed node (node + duration). |
| `IsWaitingForSignal` | `bool` (read-only) | True while running and parked on an await-signal node. |
| `CurrentAwaitSignal` | `string` (read-only) | The awaited signal name while `IsWaitingForSignal`, else `""`. |

## Lifecycle changes

```
Awake():
  if _persistAcrossScenes:
     if Active != null && Active != this:   // a duplicate per-scene copy
        Destroy(gameObject); return         // original keeps running the flow
     Active = this
     DontDestroyOnLoad(gameObject)

Start():  if _bootOnStart: Boot()           // (was: Boot())

OnDestroy():  Stop();  if Active == this: Active = null    // (was: Stop())
```

`Subscribe()` / `Unsubscribe()` gain the `OnWaitingForTime` pair:
`_runner.OnWaitingForTime += HandleWaitingForTime` / `-=`, with
`HandleWaitingForTime(node, seconds) => OnWaitingForTime?.Invoke(node, seconds)`.

## Unchanged (append-only guarantee)

`Graph`, `AutoAdvance`, `SceneLoader`, `Context`, `Runner`, `IsRunning`, `OnNodeEntered`, `OnNodeCompleted`,
`OnEnded`, `OnStuck`, `OnWaitingForSignal`, `Boot`, `Tick`, `Advance`, `RaiseSignal`, `RaiseSignal<T>`,
`Stop` — all unchanged.

## Validation / invariants

- **INV-1**: With `_persistAcrossScenes` true, the driver GameObject survives a single-mode scene load (it is
  in the DontDestroyOnLoad scene), and its in-progress flow continues.
- **INV-2**: With `_persistAcrossScenes` false (default), driver lifetime is unchanged from slice 1/2.
- **INV-3**: A second persistent driver (a per-scene duplicate) destroys itself in `Awake`; the original and
  its flow are untouched; `Active` remains the original.
- **INV-4**: `BootOnStart=false` ⇒ `Start` does not boot ⇒ an explicit `Boot()` logs no "already running"
  warning.
- **INV-5**: `OnWaitingForTime` fires (node + duration) when the flow enters a `WaitDuration` node.
- **INV-6**: `IsWaitingForSignal`/`CurrentAwaitSignal` report the parked await; false/"" before boot, after
  end, or when not awaiting a signal.
- **INV-7**: `Active` is the persistent driver after it boots; null when none persists.
- **INV-8 (regression)**: a real `start → loadA(Single) → await → loadB(Single) → end` run, via the real
  `SceneManager`, reaches `OnEnded` with the persistent driver still alive.
- **INV-9**: graphcore/graphstandard untouched; 659 EditMode + 8 prior PlayMode stay green.

## Test scenes (PlayMode)

- `Tests/PlayMode/Scenes/GameFlowCrossSceneA.unity`, `…SceneB.unity` — minimal empty scenes, committed,
  registered into Build Settings by the test's `[OneTimeSetUp]` (`#if UNITY_EDITOR`) and removed in teardown.
