# Public API Contract — ReactiveEvaluator.OnNodeLocked (graphstandard 0.6.0)

Namespace `Faolline.GraphStandard`. Additive — no existing member changes.

```csharp
public class ReactiveEvaluator
{
    public event Action<string> OnNodeAvailable;   // unchanged
    public event Action<string> OnNodeCompleted;   // unchanged

    /// <summary>Raised when a node enters <see cref="ReactiveNodeState.Locked"/>; the node id is passed.
    /// The counterpart of OnNodeAvailable/OnNodeCompleted — fires on a backward transition during
    /// Reevaluate and once per initially-Locked node during Start().</summary>
    public event Action<string> OnNodeLocked;      // NEW
    // ... all other members unchanged ...
}
```

## Behavior contract

| Scenario | Result |
|----------|--------|
| Available node, completed-set drops below `k`, `Reevaluate()` | `OnNodeLocked(id)` fires once; `GetState(id)==Locked` |
| `Start()` initial emission | `OnNodeLocked` fires for each currently-Locked node (symmetry); not for Available/Completed |
| `Reevaluate()` leaving a node unchanged | no `OnNodeLocked` for it |

## Compatibility

- **Additive only**: one new event; `OnNodeAvailable`/`OnNodeCompleted`/derivation unchanged.
- **Versioning**: graphstandard `0.5.0 → 0.6.0` (MINOR).
- **Dependencies**: graphcore only.
