# Phase 1 — Data Model: Flow engine

New `FlowRunner` in `com.faolline.graphstandard` (0.2.0 → 0.3.0). graphcore unchanged.

## `FlowRunner` — `Runtime/Flow/FlowRunner.cs`

Constructor:
- `FlowRunner(BaseGraph graph, BaseContext context, IReadOnlyCollection<string> oneShotNodeIds = null,
  IReadOnlyDictionary<string,int> joinThresholds = null, int maxFiresPerPropagation = 10000)`
  — copies the one-shot set and threshold map; precomputes per-node **incoming-edge count** (default join
  threshold) and an outgoing-edge index from `graph.Edges`.

Fields:
- `_incoming : Dictionary<string,int>` — incoming-edge count per node (default AND threshold).
- `_oneShot : HashSet<string>`, `_joinThresholds : Dictionary<string,int>` — config copies.
- `_fired : HashSet<string>` — node ids fired since the last `Reset`.
- `_arrived : Dictionary<string, HashSet<string>>` — incoming edge ids delivered toward each node.
- `_fireCount : int` — fires in the current `Fire` call (vs `_maxFires`); `_capWarned : bool`.

Public members:
- `event Action<string> OnNodeFired` — a node fired (id passed), after its enter-actions ran.
- `void Fire(string nodeId)` — external trigger: resets `_fireCount`, then fires the node (bypassing its
  join threshold for this direct fire) and cascades.
- `void Reset()` — clears `_fired` and `_arrived` (re-arms one-shots; fresh pass).
- `bool HasFired(string nodeId)` — fired since last reset.
- `IReadOnlyCollection<string> FiredNodeIds` — snapshot of `_fired`.

### Algorithm

```
Fire(id):
    _fireCount = 0; _capWarned = false
    FireNode(id)

FireNode(id):
    if id is not a node in the graph -> return
    if _oneShot.Contains(id) and _fired.Contains(id) -> return            // one-shot guard
    if ++_fireCount > _maxFires -> warn once [GraphStandard]; return       // cycle safety cap
    foreach action in node(id).OnEnterActions: action?.Execute(context)    // do the work
    _fired.Add(id); OnNodeFired?.Invoke(id)
    _arrived[id]?.Clear()                                                  // consume tokens
    foreach outgoing edge e from id:
        if e.Condition == null or e.Condition.Evaluate(context):           // gate
            set = _arrived[e.ToNodeId] ??= new HashSet<string>()
            set.Add(e.Id)                                                  // token, keyed by edge id
            if set.Count >= Threshold(e.ToNodeId): FireNode(e.ToNodeId)    // join → cascade

Threshold(node) = _joinThresholds[node] if present else _incoming[node]   // default = AND (all incoming)
```

**Invariants**
- F1 — **Fork**: firing propagates along EVERY condition-passing outgoing edge, not one (FR-001, SC-001).
- F2 — **Join**: a node fires only when its arrived-edge-id count reaches its threshold; default = incoming
  count (AND); config overrides for k-of-N / OR (FR-002, SC-002).
- F3 — **Actions + events**: firing runs `OnEnterActions` then emits `OnNodeFired` (FR-003, SC-005).
- F4 — **Condition gating**: a false edge condition delivers no token (FR-004, SC-005).
- F5 — **Re-pass**: a non-one-shot node re-fires on later propagation; `_arrived` clears on fire so AND-joins
  re-accumulate cleanly (FR-005).
- F6 — **One-shot**: a one-shot node fires at most once until `Reset` (FR-006, SC-003).
- F7 — **Cycle-bounded**: propagation stops at `_maxFires` with one `[GraphStandard]` warning (FR-005, SC-004).
- F8 — **Direct fire bypasses join**: `Fire(id)` always fires `id` (the host trigger); token delivery
  respects the threshold (FR-007).
- F9 — **Robust**: unknown id ignored; threshold > incoming ⇒ never token-fires; token at an already-fired
  one-shot ignored (FR-009, edge cases).
- F10 — **graphcore untouched**: engine reads only graphcore's public surface (FR-010, SC-007).
