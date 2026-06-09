# Public API Contract — gameflow host bridge (0.1.0)

Namespace `Faolline.GraphGameFlow`, package `com.faolline.graphgameflow`. All public members carry XML docs;
all misuse logs use the `[GraphGameFlow]` prefix; C# `Action<T>` events (no `UnityEvent`).

## GraphFlowDriver : MonoBehaviour

```csharp
public sealed class GraphFlowDriver : MonoBehaviour
{
    // Inspector
    [SerializeField] private BaseGraph _graph;
    [SerializeField] private bool _autoAdvance = true;

    // Injection seam (set before Boot; defaults to a UnitySceneLoader)
    public ISceneLoader SceneLoader { get; set; }

    // Read-only run state
    public GameFlowContext Context { get; }
    public BaseRunner Runner { get; }
    public bool IsRunning { get; }

    // Re-exposed runner lifecycle (subscribe from scene scripts)
    public event Action<BaseNodeData> OnNodeEntered;
    public event Action<BaseNodeData> OnNodeCompleted;
    public event Action<EndReason>    OnEnded;
    public event Action               OnStuck;
    public event Action<BaseNodeData, string> OnWaitingForSignal;

    // Host bridge surface (all EditMode-callable)
    public void Boot();                                  // boot runner over Graph + shared context
    public void Tick(float deltaSeconds);                // forward frame time (dt <= 0 ignored)
    public void Advance();                               // Proceed (manual or programmatic)
    public void RaiseSignal(string name);                // resume a matching await-signal node
    public void RaiseSignal<T>(string name, T payload);  // scalar-payload variant

    // Unity hooks (thin): Start()->Boot(); Update()->Tick(Time.deltaTime); OnDestroy()->unsubscribe
}
```

**Contract**

- `Boot` with a null `Graph` or no resolvable start node logs `[GraphGameFlow]` warning and leaves
  `IsRunning == false`; it does not throw.
- `Boot` builds a `GameFlowContext` whose `SceneLoader` is this driver's `SceneLoader` (default
  `UnitySceneLoader`) and an empty `NodeExecutorRegistry` (statement/await nodes need none).
- If `AutoAdvance`, the driver advances on `OnNodeCompleted`. An await-signal node parks the runner and does
  not raise `OnNodeCompleted`, so auto-advance does not skip the wait.
- `Tick`/`Advance`/`RaiseSignal` are safe no-ops before `Boot` and after `OnEnded`.
- `OnDestroy` unsubscribes from the runner; no callbacks fire afterward.

## GameFlowContext : BaseContext

```csharp
public class GameFlowContext : BaseContext
{
    public ISceneLoader SceneLoader { get; set; }
    protected override BaseContext CreateCloneInstance();   // new GameFlowContext()
    public override    BaseContext DeepClone();             // base + copy SceneLoader reference
}
```

## GameFlowContextKeys

```csharp
public static class GameFlowContextKeys
{
    // No domain keys in slice 1. New keys are declared here as const string — never inline at call sites.
}
```

## ISceneLoader / UnitySceneLoader

```csharp
public interface ISceneLoader
{
    void LoadScene(string sceneName, LoadSceneMode mode);
}

public sealed class UnitySceneLoader : ISceneLoader
{
    public void LoadScene(string sceneName, LoadSceneMode mode);  // SceneManager.LoadScene; logs on missing
}
```

**Contract**: an empty/unknown scene name logs `[GraphGameFlow]` error and returns; never throws.

## LoadSceneAction : BaseAction

```csharp
public sealed class LoadSceneAction : BaseAction
{
    [SerializeField] private string        _sceneName;
    [SerializeField] private LoadSceneMode _mode = LoadSceneMode.Single;

    public string        SceneName { get; set; }
    public LoadSceneMode Mode      { get; set; }

    public override void Execute(BaseContext context);   // resolve loader from GameFlowContext, LoadScene
}
```

**Contract**

- Resolves `(context as GameFlowContext)?.SceneLoader`, falling back to a shared default `UnitySceneLoader`.
- Behaves identically whether on a node's enter or exit list, and regardless of node type.
- Empty `SceneName` logs `[GraphGameFlow]` error; no throw.

## Semver / compatibility

- New additive package at **0.1.0**; depends on `com.faolline.graphcore` **0.6.0** (pinned).
- graphcore and graphstandard are **unchanged**; the existing 634-test EditMode suite stays green.
