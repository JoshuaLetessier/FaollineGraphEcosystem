# Public API Contract — gameflow driver cross-scene hardening (0.3.0)

Additive to `Faolline.GraphGameFlow.GraphFlowDriver`. `[GraphGameFlow]` prefix; XML docs; C# `Action<T>`.

## Added surface

```csharp
public sealed class GraphFlowDriver : MonoBehaviour
{
    // Inspector (new)
    [SerializeField] private bool _persistAcrossScenes = false;
    [SerializeField] private bool _bootOnStart        = true;

    public bool PersistAcrossScenes { get; set; }   // read at Awake
    public bool BootOnStart         { get; set; }

    /// <summary>The current persistent driver (set when one with PersistAcrossScenes boots), or null.</summary>
    public static GraphFlowDriver Active { get; private set; }

    /// <summary>Raised when the flow enters a timed node. Args: the node + the wait duration (seconds).</summary>
    public event Action<BaseNodeData, float> OnWaitingForTime;

    /// <summary>True while running and parked on an await-signal node.</summary>
    public bool IsWaitingForSignal { get; }

    /// <summary>The awaited signal name while waiting for a signal; otherwise "".</summary>
    public string CurrentAwaitSignal { get; }

    // Unity hooks (changed behavior, same signatures):
    //   Awake(): if PersistAcrossScenes → dedup vs Active, else set Active + DontDestroyOnLoad
    //   Start(): if BootOnStart → Boot()
    //   OnDestroy(): Stop(); clear Active if owned
}
```

**Contract**

- `PersistAcrossScenes` (default false) makes the driver survive single-mode scene loads; it is read in
  `Awake`, so set it before the GameObject activates (inspector value, or an inactive-GameObject set in code).
- With it on, a single driver runs a graph that spans scenes; scene scripts reach the driver via
  `GraphFlowDriver.Active`. A duplicate persistent driver (a per-scene copy) self-destructs, leaving the
  original; `Active` stays the original.
- With it off (default), lifetime is unchanged from 0.2.0.
- `BootOnStart` (default true) preserves auto-boot on Play; false lets a test/integrator configure then
  `Boot()` explicitly with no "already running" warning.
- `OnWaitingForTime` mirrors `OnWaitingForSignal`. `IsWaitingForSignal`/`CurrentAwaitSignal` are read-only
  views of the parked state (false/"" before boot, after end, or when not awaiting a signal).

## Unchanged

`Graph`, `AutoAdvance`, `SceneLoader`, `Context`, `Runner`, `IsRunning`, `OnNodeEntered`, `OnNodeCompleted`,
`OnEnded`, `OnStuck`, `OnWaitingForSignal`, `Boot`, `Tick`, `Advance`, `RaiseSignal`(+`<T>`), `Stop`.

## Semver / compatibility

- gameflow **0.2.0 → 0.3.0** (MINOR, additive). graphcore/graphstandard untouched. Slice-1/2 callers compile
  and behave identically (persist OFF by default). 659 EditMode + 8 prior PlayMode stay green; the slice adds
  EditMode tests + one real cross-scene PlayMode test.
