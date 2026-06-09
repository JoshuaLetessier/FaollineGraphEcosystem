# Quickstart — Flow engine

Multi-active fork/join execution in `com.faolline.graphstandard` 0.3.0 (depends on graphcore 0.6.0).

## 1. Author an ability flow

Edges are flow connections (fire source ⇒ propagate to target). A fork reconverges at a join:

```
        ┌─► damage ─┐
cast ───┼─► debuff ─┼─► cooldown      (cooldown joins all 3 = AND)
        └─► vfx ────┘
```

## 2. Fire it

```csharp
var flow = new FlowRunner(graph, ctx);          // default join = AND (all incoming)
flow.OnNodeFired += id => Debug.Log($"fired: {id}");
flow.Fire("cast");                              // → cast, damage, debuff, vfx, then cooldown (once)
```

Firing a node runs its `OnEnterActions` (so it can mutate the context) and emits `OnNodeFired`; it then
propagates along every outgoing edge whose `Condition` passes (fork). `cooldown` fires only after all three
effects arrive (join).

## 3. Configure joins, one-shots, and the safety cap

```csharp
var flow = new FlowRunner(
    graph, ctx,
    oneShotNodeIds: new[] { "vfx" },                       // vfx fires at most once until Reset
    joinThresholds: new Dictionary<string,int> { ["cooldown"] = 1 },  // OR-join: fire on first arrival
    maxFiresPerPropagation: 10000);                        // cycle safety cap
```

| Join threshold for a node | Meaning |
|---------------------------|---------|
| default (= incoming count)| AND — all branches must arrive |
| 1                         | OR — first arrival fires it |
| k                         | k-of-N |

## 4. Re-pass, one-shot, reset

```csharp
flow.Fire("cast");      // first pass
flow.Fire("cast");      // re-pass: non-one-shot nodes fire again; one-shot nodes skip
flow.Reset();           // clear fired/token state — re-arms one-shots for a fresh cast
```

## 5. Key rules

- **Fork** = activate ALL valid edges (vs. the Linear runner's select-one).
- **Join** = k-of-N rendezvous (default AND); `Fire(id)` directly bypasses the join for that node.
- **Re-pass** is intentional; **cycles** are allowed but bounded by the fire cap (a `[GraphStandard]` warning).
- **One-shot** nodes fire once until `Reset`.
- **graphcore is untouched** — Flow is a graphstandard engine over the shared substrate.

## 6. Verify

graphstandard EditMode tests cover fork, conditional fork, chains, AND/OR/k-of-N joins, re-pass, one-shot,
`Reset`, the cycle cap, action-mutates-context, and a full ability-cast scenario — all headless.
