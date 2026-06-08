# Quickstart — P2 Context collections

How a **graph author** holds set-valued state, how a **host integrator** saves it, and how the graphTest
authoring nodes use it. (graphcore 0.5.0; headless / EditMode-testable.)

## 1. Hold and query a set

```csharp
var ctx = new BaseContext();
ctx.AddToCollection("inventory", "key");
ctx.AddToCollection("inventory", "rope");
ctx.AddToCollection("inventory", "key");          // idempotent — still 2

ctx.CollectionCount("inventory");                 // 2
ctx.CollectionContains("inventory", "key");       // true
ctx.RemoveFromCollection("inventory", "key");     // now {"rope"}
foreach (var item in ctx.GetCollection("inventory")) { /* "rope" */ }
ctx.ClearCollection("inventory");                 // empty
```

Collections have their own keyspace — a scalar `Set<int>("inventory", 5)` and the `"inventory"` collection
coexist without interfering.

## 2. React to changes

```csharp
ctx.OnCollectionChanged("solved", key => Debug.Log($"'{key}' changed → count {ctx.CollectionCount(key)}"));
ctx.AddToCollection("solved", "p1");   // fires (new)
ctx.AddToCollection("solved", "p1");   // silent (no real change)
ctx.RemoveFromCollection("solved", "p1"); // fires
```

A notification fires only on a **real** membership change and carries the collection key; re-query the set
in the handler. (This is the hook a future Reactive evaluator subscribes to.)

## 3. Persist and restore

```csharp
// Save: collections are exposed in parallel to scalars
IReadOnlyDictionary<string, IReadOnlyCollection<string>> sets = ctx.GetAllCollections();
IReadOnlyDictionary<string, object> scalars = ctx.GetAllParameters();  // unchanged, scalar-only
// (the save layer composes both into one blob)

// History: collections are deep-copied, so step-back restores exact membership
var snapshot = ctx.DeepClone();   // independent copies
```

Collections are **durable** (unlike P1 signals, which are transient and excluded from save/history).

## 4. Global, not scoped

```csharp
ctx.BeginLocalContext();             // scalar overlay only
ctx.AddToCollection("solved", "p2"); // targets the GLOBAL collection store
ctx.EndLocalContext();
ctx.CollectionContains("solved", "p2"); // still true — collections are global
```

## 5. Author with collections (graphTest)

- **Membership gate** — `TestCollectionContainsCondition { Key="inventory", Item="key" }` makes an edge
  traversable only when the set contains the item.
- **Count-threshold gate** — `TestCollectionCountCondition { Key="collected", Operator=GreaterOrEqual,
  Value=3 }` passes when at least 3 ids were collected.
- **Recipe** — `TestRecipeAction { Key="inventory", Required={"x","y"}, Reward="z" }`: on a context holding
  both `x` and `y`, running it removes them and adds `z`; missing a required element ⇒ no change.

## 6. Key rules

- **String elements, set semantics** (no duplicates, no ordering); lists/multisets and non-string elements
  are out of scope for v1.
- **Durable** (save + history), **global-only** (not in the local overlay).
- **Notifications fire on real change only.**
- **No collections used ⇒ nothing changes**: identical to graphcore 0.4.0.

## 7. Verify in graphTest

`com.faolline.graphTest` exercises the full surface: membership-gated and count-threshold-gated edges, the
recipe consume→produce action, durability, and the no-collections back-compat path — all EditMode, editor
closed.
