# Phase 1 — Data Model: collection primitives

Three additive ScriptableObject types in `Faolline.GraphStandard`. No changes to graphcore / gameflow / graphTest.

## AddToCollectionAction : BaseAction

| Member | Kind | Description |
|--------|------|-------------|
| `_collectionKey` / `CollectionKey` | `[SerializeField] string` + prop | The collection key to write to. |
| `_value` / `Value` | `[SerializeField] string` + prop | The value to add. |
| `Execute(BaseContext)` | override | `if key/value non-empty: context.AddToCollection(key, value)` — else no-op. |
| `[CreateAssetMenu]` | attribute | "GraphStandard/Actions/Add To Collection". |

## CollectionContainsCondition : BaseCondition

| Member | Kind | Description |
|--------|------|-------------|
| `_collectionKey` / `CollectionKey` | `[SerializeField] string` + prop | The collection inspected. |
| `_value` / `Value` | `[SerializeField] string` + prop | The membership tested. |
| `Evaluate(BaseContext)` | override | `return context.CollectionContains(key, value);` |
| `[CreateAssetMenu]` | attribute | "GraphStandard/Conditions/Collection Contains". |

## CollectionCountAtLeastCondition : BaseCondition

| Member | Kind | Description |
|--------|------|-------------|
| `_collectionKey` / `CollectionKey` | `[SerializeField] string` + prop | The collection whose cardinality is read. |
| `_threshold` / `Threshold` | `[SerializeField] int` + prop | The minimum count to satisfy. |
| `Evaluate(BaseContext)` | override | `return context.CollectionCount(key) >= threshold;` |
| `[CreateAssetMenu]` | attribute | "GraphStandard/Conditions/Collection Count At Least". |

## Validation / invariants

- **INV-1**: After `AddToCollectionAction(K,V).Execute(ctx)`, `ctx.CollectionContains(K,V)` is true; a second
  Execute leaves `ctx.CollectionCount(K) == 1` (idempotent set).
- **INV-2**: `AddToCollectionAction` with empty/whitespace key or value makes no change.
- **INV-3**: `CollectionContainsCondition(K,V).Evaluate(ctx)` ⇔ `ctx` collection `K` contains `V` (false for an
  absent key).
- **INV-4**: `CollectionCountAtLeastCondition(K,N).Evaluate(ctx)` ⇔ `ctx.CollectionCount(K) >= N`; `N==0` ⇒ true
  for any (incl. absent) collection; `N>0` ⇒ false for an absent/empty collection.
- **INV-5 (pattern)**: with prerequisite nodes writing ids into `"completed"` via `AddToCollectionAction`, a
  `ReactiveEvaluator(graph, ctx, "completed")` bridged by `ctx.OnCollectionChanged("completed", _ => ev.Reevaluate())`
  reports a downstream node (`requiredCounts[d]=k`) Available exactly once `k` of its prerequisites are recorded.
- **INV-6**: graphcore / gameflow / graphTest untouched; existing suites green; graphstandard `0.4.0 → 0.5.0`.
