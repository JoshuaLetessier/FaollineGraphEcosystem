# Phase 1 — Data Model: gameflow host bridge

All new types live in `com.faolline.graphgameflow` (namespace `Faolline.GraphGameFlow`). graphcore types are
referenced, never modified.

## GraphFlowDriver (MonoBehaviour)

The host bridge. Binds a graph asset + shared context to the Unity runtime and drives the Linear runner.

| Member | Kind | Description |
|--------|------|-------------|
| `Graph` | `[SerializeField] BaseGraph` | The flow to run (assigned in the inspector). |
| `AutoAdvance` | `[SerializeField] bool` (default `true`) | When true, advances automatically on `OnNodeCompleted`; when false, only `Advance()` advances. |
| `SceneLoader` | `ISceneLoader` (property, default `UnitySceneLoader`) | Injected before `Boot` in tests; placed on the context at boot. |
| `Context` | `GameFlowContext` (read-only after boot) | The single shared blackboard for the run. |
| `Runner` | `BaseRunner` (read-only) | The graphcore runner the driver drives. |
| `IsRunning` | `bool` | True between a successful `Boot` and `OnEnded`. |
| `OnNodeEntered` | `event Action<BaseNodeData>` | Re-exposed from the runner. |
| `OnNodeCompleted` | `event Action<BaseNodeData>` | Re-exposed from the runner. |
| `OnEnded` | `event Action<EndReason>` | Re-exposed from the runner. |
| `OnStuck` | `event Action` | Re-exposed from the runner. |
| `OnWaitingForSignal` | `event Action<BaseNodeData, string>` | Re-exposed; the flow parked awaiting a signal. |
| `Boot()` | `void` | Builds context (with `SceneLoader`) + empty `NodeExecutorRegistry`, subscribes, `runner.Start`. Warns `[GraphGameFlow]` and stays inert if `Graph` is null or has no valid start. Idempotent guard: a second `Boot` while running is a no-op + warning. |
| `Tick(float dt)` | `void` | Forwards to `runner.Tick(dt)` while running; `dt <= 0` ignored. |
| `Advance()` | `void` | `runner.Proceed()` while running; no-op otherwise. |
| `RaiseSignal(string name)` | `void` | `runner.RaiseSignal(name)` while running; no-op otherwise. |
| `RaiseSignal<T>(string name, T payload)` | `void` | Scalar-payload variant. |
| `Start()` *(Unity)* | `void` | Calls `Boot()`. |
| `Update()` *(Unity)* | `void` | Calls `Tick(Time.deltaTime)`. |
| `OnDestroy()` *(Unity)* | `void` | Unsubscribes from the runner; no dangling callbacks. |

**Lifecycle / state**

```
(disabled) ──Play/Start──► Boot ──► [Running] ──Update→Tick──► ... ──OnEnded──► [Ended]
                              │                    ▲   │
                       no graph/start              │   └─ await node ⇒ parked until RaiseSignal(match)
                              ▼                     │
                          [Inert] (warned)          └─ auto-advance on OnNodeCompleted (if AutoAdvance)
```

## GameFlowContext : BaseContext

Typed context for the host layer (Constitution VI). For this slice it carries the scene-loader service; it
is the shared context future slices (Reactive/Flow) will extend.

| Member | Kind | Description |
|--------|------|-------------|
| `SceneLoader` | `ISceneLoader` (property) | The active scene loader; read by `LoadSceneAction`. Not a `BaseContext` parameter (it is a service object, not bool/int/float/string). |
| `CreateCloneInstance()` | `override BaseContext` | Returns `new GameFlowContext()` — required so history snapshots restore the right type. |
| `DeepClone()` | `override BaseContext` | `base.DeepClone()` then copies the `SceneLoader` reference (a shared service, not per-snapshot state). |

## GameFlowContextKeys

Static keys class (Constitution VI). **Empty placeholder for this slice** — no domain string keys exist yet
(the scene name is a serialized field on the action, not a context key). The first domain key added in a
later slice goes here; raw key literals never appear at call sites.

## ISceneLoader (seam)

```
void LoadScene(string sceneName, LoadSceneMode mode);
```

The single point Unity scene loading flows through, so the driver wiring and the scene-flow logic are
EditMode-testable.

- **UnitySceneLoader** — default production impl: `SceneManager.LoadScene(sceneName, mode)`; logs a
  `[GraphGameFlow]` error if the scene is not in build settings rather than throwing.
- **StubSceneLoader** *(test only, EditMode)* — records `(sceneName, mode)` calls for assertions; loads
  nothing.

## LoadSceneAction : BaseAction

A graphcore action (ScriptableObject). Attachable to **any** node's `OnEnterActions` or `OnExitActions`.

| Member | Kind | Description |
|--------|------|-------------|
| `SceneName` | `[SerializeField] string` | Target scene (by name). |
| `Mode` | `[SerializeField] LoadSceneMode` (default `Single`) | Single or Additive. |
| `Execute(BaseContext context)` | `override void` | Resolves `(context as GameFlowContext)?.SceneLoader` (or a shared default `UnitySceneLoader`) and calls `LoadScene(SceneName, Mode)`. Empty `SceneName` ⇒ `[GraphGameFlow]` error, no throw. |

## Reference scene-flow graph (test fixture)

```
StartNode("start")
  └─► StatementNode("loadA")   OnEnterActions: LoadSceneAction{ SceneName="A", Single }
        └─► StatementNode("gate")  AwaitSignalName = "advance"
              └─► StatementNode("loadB")  OnEnterActions: LoadSceneAction{ SceneName="B", Single }
                    └─► EndNode("end", Completed)
```

Driven by one `GraphFlowDriver` over one `GameFlowContext`. In EditMode the loader is a `StubSceneLoader`
(assert A then B recorded, B only after the signal); in PlayMode a minimal variant proves the real
`UnitySceneLoader` path.

## Validation / invariants

- **INV-1**: No `Boot` without a non-null `Graph` and a resolvable start node (else inert + warning).
- **INV-2**: `Tick` with `dt <= 0` changes nothing.
- **INV-3**: `RaiseSignal` only advances a flow parked on a matching `AwaitSignalName`; otherwise a no-op.
- **INV-4**: `Advance`/`RaiseSignal`/`Tick` before `Boot` or after `OnEnded` never throw.
- **INV-5**: `LoadSceneAction` behaves identically regardless of host node type or enter/exit list.
- **INV-6**: A destroyed/disabled driver leaves no live runner subscription (no callbacks after `OnDestroy`).
- **INV-7**: graphcore/graphstandard unchanged; the 634-test EditMode suite stays green.
