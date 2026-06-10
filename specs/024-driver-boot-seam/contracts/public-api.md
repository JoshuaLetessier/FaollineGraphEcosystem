# Public API Contract — driver boot configuration seam (gameflow 0.5.0)

Additive to `Faolline.GraphGameFlow.GraphFlowDriver`.

```csharp
public sealed class GraphFlowDriver : MonoBehaviour
{
    // … slice 1–4 members unchanged …

    /// <summary>Boots on a CALLER-SUPPLIED context and registry — prepare shared state and custom executors
    /// before the flow starts. A null context falls back to a fresh graph-initialised one; a null registry to
    /// an empty one. A supplied context is used as-is (NOT re-initialised from the graph), with its SceneLoader
    /// filled only when absent.</summary>
    public void Boot(GameFlowContext context, NodeExecutorRegistry registry);

    public void Boot();   // unchanged: fresh context (SceneLoader set + InitFromGraph), empty registry
}
```

**Contract**

- `Boot(context, registry)`: the flow runs on `context` (the live blackboard); the driver fills
  `context.SceneLoader` only when it is null, and does **not** `InitFromGraph` (the caller owns seeding).
  `registry` makes custom node executors active. Either argument may be null to take that side's default.
- `Boot()` is byte-for-byte the prior behavior (equivalent to `Boot(null, null)`).
- The same boot guards apply to both: no graph / no valid start / already running → the same `[GraphGameFlow]`
  warnings, stay inert.

**Example** (the seam the next slice uses):

```csharp
var ctx = new GameFlowContext();
ctx.AddToCollection("completed", "objectiveA");   // pre-seed shared state
var registry = new NodeExecutorRegistry();
registry.Register(new MyNodeExecutor());           // custom node behavior

driver.Boot(ctx, registry);                        // the flow runs on this prepared context
// later: a ReactiveEvaluator / FlowRunner can read/write the SAME ctx the driver runs.
```

## Semver / compatibility

- gameflow **0.4.0 → 0.5.0** (MINOR, additive: one new overload). graphcore/graphstandard untouched. `Boot()`
  and every other member unchanged; the 667 EditMode + 9 PlayMode tests stay green.
