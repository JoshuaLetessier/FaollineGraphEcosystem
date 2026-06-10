# Public API Contract — collection primitives (graphstandard 0.5.0)

All in namespace `Faolline.GraphStandard`, assembly `com.faolline.graphstandard.Runtime`. Additive — no existing
type changes.

```csharp
namespace Faolline.GraphStandard
{
    /// <summary>
    /// Node action that records a configured value into a configured collection on the run context.
    /// Idempotent (graphcore collections are string-sets); a graceful no-op when key or value is empty.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Actions/Add To Collection", fileName = "AddToCollectionAction")]
    public class AddToCollectionAction : BaseAction
    {
        public string CollectionKey { get; set; }
        public string Value { get; set; }
        public override void Execute(BaseContext context); // context.AddToCollection(key, value) when both non-empty
    }

    /// <summary>Condition satisfied when the collection at <c>CollectionKey</c> contains <c>Value</c>.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class CollectionContainsCondition : BaseCondition
    {
        public string CollectionKey { get; set; }
        public string Value { get; set; }
        public override bool Evaluate(BaseContext context); // context.CollectionContains(key, value)
    }

    /// <summary>Condition satisfied when the collection at <c>CollectionKey</c> holds at least <c>Threshold</c> values.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Collection Count At Least", fileName = "CollectionCountAtLeastCondition")]
    public class CollectionCountAtLeastCondition : BaseCondition
    {
        public string CollectionKey { get; set; }
        public int Threshold { get; set; }
        public override bool Evaluate(BaseContext context); // context.CollectionCount(key) >= threshold
    }
}
```

## Behavior contract

| Call | Result |
|------|--------|
| `AddToCollectionAction{K,V}.Execute(ctx)` | `ctx` collection `K` contains `V`; second call → still one `V`. |
| `AddToCollectionAction{"",V}` or `{K,""}` `.Execute(ctx)` | no change. |
| `CollectionContainsCondition{K,V}.Evaluate(ctx)` | `true` ⇔ `K` contains `V` (false for absent `K`). |
| `CollectionCountAtLeastCondition{K,N}.Evaluate(ctx)` | `true` ⇔ `CollectionCount(K) >= N`; `N=0` always true. |

## Compatibility

- **Additive only**: three new types; no signature changes to existing graphstandard / graphcore / gameflow /
  graphTest members. graphTest collection fixtures remain.
- **Versioning**: graphstandard `0.4.0 → 0.5.0` (MINOR).
- **Dependencies**: graphcore only (`BaseAction`, `BaseCondition`, `BaseContext` collection API).
