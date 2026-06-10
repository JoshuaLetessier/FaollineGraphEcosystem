# Quickstart — reacting to unlock and re-lock

`ReactiveEvaluator` now raises all three state transitions, so UI can paint from events alone:

```csharp
eval.OnNodeAvailable += id => SetIndicator(id, "available");
eval.OnNodeCompleted += id => SetIndicator(id, "done");
eval.OnNodeLocked    += id => SetIndicator(id, "locked");   // NEW — symmetric re-lock
eval.Start();   // initial emission paints every node in its current state
```

On a replay/step-back, when the completed-set shrinks and you call `Reevaluate()`, a node that drops below its
threshold raises `OnNodeLocked` — no manual repaint needed.

## Recording completion — one path, not two

Pick **one**:

- **Own the evaluator (simplest):** call `eval.MarkCompleted(id)` — it records into the completed-set **and**
  re-derives for you. Use this when your code drives completion.
- **A Linear flow writes the set:** put an `AddToCollectionAction { completed, id }` on the node and bridge
  with `ctx.OnCollectionChanged("completed", _ => eval.Reevaluate())`.

> Do **not** combine them: `MarkCompleted` already re-derives, so also wiring the bridge double-evaluates.
