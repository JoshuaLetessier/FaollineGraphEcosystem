# Quickstart — collections + hosting a reactive progression

## 1. Record and gate on a collection from a graph (no code)

- Add an **Add To Collection** action (GraphStandard/Actions) to a node's on-enter (or on-exit) list; set
  *Collection Key* (e.g. `completed`) and *Value* (e.g. the step's id). Entering the node records it. Re-entry
  does not duplicate it.
- Gate an edge with **Collection Contains** (key + value) or **Collection Count At Least** (key + threshold).
  The count-at-least condition is how "k of these done unlocks this" is expressed:
  `Collection Count At Least { completed, 2 }` opens the edge once two ids are in `completed`.

## 2. Host a reactive progression on the shared context

The Linear flow records completion; a `ReactiveEvaluator` derives the unlocks on the **same** context. The only
code you write is a two-line bridge.

```csharp
// progressionGraph: edges encode prerequisites (A→C means "C requires A").
// requiredCounts: how many prerequisites each node needs (k-of-N); omit a node for AND.
var ctx = new GameFlowContext();
var evaluator = new ReactiveEvaluator(
    progressionGraph, ctx, "completed",
    requiredCounts: new Dictionary<string, int> { ["exit"] = 2 });   // 2-of-3 unlocks "exit"

evaluator.OnNodeAvailable += id => Debug.Log($"available: {id}");

// Bridge: when the Linear flow's AddToCollectionAction writes into "completed", re-derive.
ctx.OnCollectionChanged("completed", _ => evaluator.Reevaluate());

driver.BootOnStart = false;
driver.Boot(ctx);            // slice-5 seam: the flow runs on the SAME ctx
evaluator.Start();           // initial emission after subscribing
```

Now a node in the Linear flow that carries `AddToCollectionAction{ completed, "roomA" }` records `roomA` on
entry; the bridge calls `Reevaluate`; once two ids are present, `exit` becomes Available and `OnNodeAvailable`
fires. A Linear edge may *also* gate directly with `CollectionCountAtLeastCondition{ completed, 2 }`.

## Why it matters

This composes the three pieces already in the ecosystem — the **Linear driver** (gameflow), the **collection
primitives** (this slice), and the **ReactiveEvaluator** (graphstandard) — into a live progression on one shared
blackboard, with no bespoke action, condition, or engine. The optional turnkey wrapper that owns the evaluator
and auto-bridges is deferred until a real consumer shows the two-line bridge is a burden.
