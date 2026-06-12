# com.faolline.graphgameflow

**Version**: 0.6.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` 0.7.0

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
- **AutoAdvance** (default on) walks the flow node-to-node; turn it off to advance only on `Advance()`. A
  **choice** node is never auto-resolved — it pauses for a deliberate `ChooseById(id)` (re-exposed like
  `Advance`), so a host can present options and pick a branch. This makes "render an embedded dialogue
  subgraph" a ~10-line composition: resolve the host runner's current node with graphdialoguesystem's
  `DialoguePresenter`, then drive `Advance()` (lines, with AutoAdvance off) / `ChooseById(id)` (choices).
- **Re-exposes** the runner's lifecycle as C# events (no `UnityEvent`): `OnNodeEntered`, `OnNodeCompleted`,
  `OnEnded`, `OnStuck`, `OnWaitingForSignal`.
- **Stop()** detaches from the runner (called by `OnDestroy`); also callable to halt a flow.

All logic lives in the public methods (`Boot`/`Tick`/`Advance`/`RaiseSignal`/`Stop`); the Unity hooks are
thin wrappers, so the whole bridge is verifiable in EditMode.

Re-exposed events also include **`OnWaitingForTime`** (the flow entered a timed node — node + duration),
symmetric with `OnWaitingForSignal`. A scene that subscribes *after* a wait already fired (e.g. it loaded
mid-flow) can recover the parked state: **`IsWaitingForSignal`** / **`CurrentAwaitSignal`** for a signal wait,
and **`IsWaitingForTime`** / **`WaitRemaining`** / **`WaitTotal`** for a timed wait (drive a synced
countdown: `if (flow.IsWaitingForTime) label.text = $"{flow.WaitRemaining:0.0}s";`). Set
**`BootOnStart = false`** to stop auto-boot on Play and `Boot()` explicitly after configuring the driver.

**Prepare the context / register executors before boot.** `Boot(GameFlowContext context, NodeExecutorRegistry
registry)` runs the flow on a context **you** built and seeded (collections, parameters, services) and a
registry of **your** node executors. A supplied context is used as-is (not re-initialised from the graph, so
your seeds survive; its scene loader is filled only when absent); nulls fall back to the defaults (`Boot()` is
`Boot(null, null)`). This is the seam for hosting a progression/ability system on the driver's shared context —
build the context, hand it to `Boot`, then wire a `ReactiveEvaluator` or `FlowRunner` onto that same context.

> **Looping game-shell**: a menu → play → win → back-to-menu shell never "ends" — model it as a cyclic Linear
> graph with **no End node** (the runner follows the single out-edge and loops; the flow stays running, no
> `OnEnded`). Set a small `BaseGraph.HistoryDepth` for a forever-looping shell. Build it fluently with
> graphstandard's `GraphBuilder`.

---

## Running a flow across scenes (important)

One driver running **one graph that spans scenes** must outlive scene loads — a `LoadScene(Single)` tears the
current scene down, and with it a driver that lives in that scene, mid-flow. Tick **Persist Across Scenes**
(or place the driver on your own `DontDestroyOnLoad` bootstrap, e.g. shared with a save system):

```csharp
[SerializeField] private GraphFlowDriver _flow;   // PersistAcrossScenes = true in the inspector

// Scene scripts in the loaded scenes reach the persistent driver without their own singleton:
void Start()
{
    var flow = GraphFlowDriver.Active;
    flow.OnWaitingForSignal += (n, sig) => { /* enable the door, etc. */ };
    if (flow.IsWaitingForSignal && flow.CurrentAwaitSignal == "door") EnableDoor();  // recover a missed wait
}
```

- `PersistAcrossScenes` (default off) calls `DontDestroyOnLoad` so the driver and its in-progress flow survive
  single-mode loads. A duplicate per-scene copy self-destructs, leaving the original running.
- `GraphFlowDriver.Active` is the current persistent driver.
- **Bigger games**: keep the master flow lean and model a **room / dialogue / ability as a SubGraph node**
  (graphcore's `SubGraphNodeData` → a `BaseGraph`); the substrate already nests graphs with cycle detection.

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

## Authoring in the editor

You don't have to build graphs in code. The package ships a visual editor mirroring the StarterGraph editor.

- **Create a graph**: Assets ▸ Create ▸ **GraphGameFlow ▸ Game Flow Graph**.
- **Open it**: double-click the asset → the gameflow editor window. Right-click the canvas to **Add Start /
  Statement / Choice / SubGraph / End Node**; drag ports to connect. The first Start becomes the entry node.
  **Save** (Ctrl+S); **Validate** checks the structure.
- **Configure a node** in the inspector: **Node Properties** (title, checkpoint, entry conditions, and the
  **On Enter / On Exit Actions** lists — drop a **Load Scene** action here), and a **Flow** foldout for the
  **await-signal name** and **wait duration**.
- **Create a Load Scene action**: Assets ▸ Create ▸ **GraphGameFlow ▸ Actions ▸ Load Scene**, then drop it
  into a node's action list. Scene change is always an action, never a node type. On the action's inspector
  you **pick the scene from a dropdown of the project's scenes** (no typing); a hint explains **Single**
  (replaces the current scene) vs **Additive** (loads on top). A scene must be in **Build Settings** to load
  at runtime — the inspector warns when it isn't and offers a one-click **Add to Build Settings**.
- **One-click sample**: **Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample** generates the flow
  `start → load A → await "advance" → load B → end`. Assign it to a `GraphFlowDriver` and Play.

> **The sample is a logic demo.** Its two Load Scene actions point at placeholder scenes named `A` / `B`,
> which are not in your project. When you Play, the flow runs correctly (it loads-attempts A, parks on the
> await, and on `RaiseSignal("advance")` loads-attempts B) but the scene loader logs a *graceful*
> `[GraphGameFlow]` error and skips the missing scenes — by design, it never crashes the flow. To see real
> scene transitions, select each Load Scene sub-asset and pick one of **your** scenes from the dropdown (and
> add it to Build Settings if prompted).

Running a flow is the `GraphFlowDriver` in Play; there is no in-editor runner in this version.

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
