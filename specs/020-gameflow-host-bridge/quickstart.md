# Quickstart — gameflow host bridge

Run a graph in a Unity scene with the host bridge.

## 1. Add the driver to a scene

1. Create an empty GameObject, add **GraphFlowDriver**.
2. Assign a **Graph** asset (a `BaseGraph`).
3. Leave **AutoAdvance** on for a flow that walks itself; turn it off to advance on player input.
4. Press **Play** — the driver boots the runner and enters the start node.

## 2. Make a node change the scene (an action, not a node)

A scene change is an ordinary graphcore **action**, attachable to any node's enter or exit list:

```csharp
var loadB = ScriptableObject.CreateInstance<LoadSceneAction>();
loadB.SceneName = "Level_02";
loadB.Mode      = LoadSceneMode.Single;

someNode.OnEnterActions.Add(loadB);   // entering someNode loads Level_02
// — or someNode.OnExitActions.Add(loadB) to load it when leaving the node.
```

It works the same on a statement, a choice, or a subgraph node. There is no dedicated "scene node".

## 3. Drive the flow from scene code

```csharp
[SerializeField] private GraphFlowDriver _flow;

void OnEnable()
{
    _flow.OnNodeEntered      += n => Debug.Log($"entered {n.Id}");
    _flow.OnWaitingForSignal += (n, sig) => Debug.Log($"waiting for '{sig}'");
    _flow.OnEnded            += reason => Debug.Log($"flow ended: {reason}");
}

// A door, a button, a trigger volume resumes a flow parked on an await-signal node:
public void OnDoorOpened() => _flow.RaiseSignal("advance");

// Manual-advance mode: progress when the player acts.
public void OnContinuePressed() => _flow.Advance();
```

## 4. The reference scene-flow

```
start → [enter: LoadScene "A"] → (await "advance") → [enter: LoadScene "B"] → end
```

- Play: scene **A** loads on entering the first node.
- The flow parks on the await-signal node (`AwaitSignalName = "advance"`).
- Scene code calls `_flow.RaiseSignal("advance")` → the flow resumes and scene **B** loads.
- All over one shared `GameFlowContext`.

## Testing notes

- **EditMode** (fast, deterministic): inject a `StubSceneLoader` before `Boot`, then call `Boot()`,
  `Tick(dt)`, `RaiseSignal("advance")`, `Advance()` directly and assert recorded scene loads + event order.
  This covers all driver wiring and the full A→await→B logic without entering Play.
- **PlayMode**: a minimal test proves the real `UnitySceneLoader` → `SceneManager` path (a node enter-action
  loads a tiny test scene additively under the live `Update` pump).
