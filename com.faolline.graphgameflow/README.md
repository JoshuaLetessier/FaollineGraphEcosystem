# com.faolline.graphgameflow

**Version**: 0.1.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` 0.6.0

The **orchestrator / host layer** of the Faolline graph ecosystem. graphcore and graphstandard are strictly
**headless** (no `MonoBehaviour`, no scene knowledge); graphgameflow is the adapter that **runs** those graphs
inside a live Unity scene. It is the one ecosystem layer where Unity-specific vocabulary
(`MonoBehaviour`, `SceneManager`, the per-frame tick, graph assets) is intentionally allowed — that binding
is the package's reason to exist.

> This is the inverse of the *Universal-Abstractions-Only* rule that governs the libs beneath it, and it is
> deliberate: the headless foundation stays portable, gameflow is where it meets the Unity runtime.

---

## Architecture

```
com.faolline.graphgameflow
│
├── Runtime/
│   ├── Context/
│   │   ├── GameFlowContext       BaseContext subclass; the shared blackboard, carries the scene loader
│   │   └── GameFlowContextKeys   companion keys class (Constitution VI)
│   ├── Scene/
│   │   ├── ISceneLoader          seam: LoadScene(name, mode)
│   │   ├── UnitySceneLoader      default impl → UnityEngine.SceneManagement
│   │   └── LoadSceneAction       a graphcore BaseAction (NOT a node type)
│   └── Driver/
│       └── GraphFlowDriver       the MonoBehaviour host bridge
│
└── Tests/
    ├── EditMode/                 driver wiring, scene action, full A→await→B flow (stub loader)
    └── PlayMode/                 the real Unity pump (Start/Update) driving the bridge end to end
```

---

## GraphFlowDriver — the host bridge

A scene component that runs a graph. Drop it on a GameObject, assign a `BaseGraph`, press Play.

```csharp
[SerializeField] private GraphFlowDriver _flow;

void OnEnable()
{
    _flow.OnNodeEntered      += n => Debug.Log($"entered {n.Id}");
    _flow.OnWaitingForSignal += (n, sig) => Debug.Log($"waiting for '{sig}'");
    _flow.OnEnded            += reason => Debug.Log($"ended: {reason}");
}

// from a button / trigger / input handler:
public void Continue()  => _flow.Advance();            // manual advance
public void OpenDoor()  => _flow.RaiseSignal("door");  // resume a flow parked on an await-signal node
```

- **Boots itself** on `Start` (or call `Boot()` from code). No graph / no valid start ⇒ a `[GraphGameFlow]`
  warning and it stays inert (never throws).
- **Pumps time**: `Update` forwards `Time.deltaTime` to the runner, so time-wait nodes resolve. Pausing is
  simply not ticking (disable the component); `dt ≤ 0` is ignored.
- **AutoAdvance** (default on) walks the flow node-to-node; turn it off to advance only on `Advance()`.
- **Re-exposes** the runner's lifecycle as C# events (no `UnityEvent`): `OnNodeEntered`, `OnNodeCompleted`,
  `OnEnded`, `OnStuck`, `OnWaitingForSignal`.
- **Stop()** detaches from the runner (called by `OnDestroy`); also callable to halt a flow.

All logic lives in the public methods (`Boot`/`Tick`/`Advance`/`RaiseSignal`/`Stop`); the Unity hooks are
thin wrappers, so the whole bridge is verifiable in EditMode.

---

## Scene transitions are an action, not a node

A scene change is an ordinary graphcore **action** — attach it to **any** node's enter *or* exit list (a
statement, a choice, a subgraph node…), exactly like any other action. There is no dedicated "scene node":
that would be redundant with the action model and non-composable.

```csharp
var load = ScriptableObject.CreateInstance<LoadSceneAction>();
load.SceneName = "Level_02";
load.Mode      = LoadSceneMode.Single;     // or Additive

someNode.OnEnterActions.Add(load);          // load on entering the node
// — or someNode.OnExitActions.Add(load);   // load on leaving it
```

`LoadSceneAction` resolves the active `ISceneLoader` from the running `GameFlowContext` (defaulting to a
`UnitySceneLoader`). A missing/empty scene logs a `[GraphGameFlow]` error and the flow continues.

---

## The reference scene-flow

```
start → [enter: LoadScene "A"] → (await "advance") → [enter: LoadScene "B"] → end
```

Play loads A; the flow parks on the await-signal node; scene code calls `_flow.RaiseSignal("advance")` and
the flow resumes and loads B — all over one shared `GameFlowContext`.

---

## Testing the bridge

- **EditMode** (fast, deterministic): inject a recording `ISceneLoader`, then call `Boot()`, `Tick(dt)`,
  `RaiseSignal(...)`, `Advance()` directly and assert recorded loads + event order. The whole A→await→B logic
  is covered here without entering Play.
- **PlayMode**: proves the real Unity lifecycle — `Start`→`Boot`, `Update`→`Tick` over real frames — drives
  the bridge end to end. The literal `SceneManager.LoadScene` call is Unity's own API (in `UnitySceneLoader`).

---

## Constraints

- graphcore and graphstandard are **never modified** — gameflow is additive, depending only on graphcore.
- Unity-specific concerns are **confined to this package** (`MonoBehaviour`, `SceneManager`, the frame tick).
- `[GraphGameFlow]` log prefix on misuse; one class per file; XML docs; C# `Action<T>` (no `UnityEvent`).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
