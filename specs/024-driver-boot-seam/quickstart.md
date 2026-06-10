# Quickstart — preparing the driver's context before boot

By default the driver creates its own context and an empty executor registry. To **prepare shared state** or
**register custom node executors** first, build them and pass them to `Boot`:

```csharp
// 1. Build and seed the shared context.
var ctx = new GameFlowContext();
ctx.Set<int>("lives", 3);
ctx.AddToCollection("completed", "intro");          // e.g. a progression completed-set
// ctx.SceneLoader is filled by the driver if you leave it null.

// 2. Register custom node executors (optional).
var registry = new NodeExecutorRegistry();
registry.Register(new MyCustomNodeExecutor());

// 3. Boot on them.
driver.BootOnStart = false;   // configure first, boot explicitly
driver.Boot(ctx, registry);
```

- The flow runs on **`ctx`** — actions read/write the values you seeded; the driver does **not** re-initialise
  it from the graph (your seeds survive).
- A null context or registry takes the default (`Boot()` is exactly `Boot(null, null)`).

## Why it matters

This is the seam for **hosting a progression/ability system on the driver's shared context**: build the
context, seed it, hand it to `Boot`, then wire a `ReactiveEvaluator` (objectives) or a `FlowRunner` (abilities)
onto that **same** `ctx` — the Linear flow and the engines share one blackboard.
