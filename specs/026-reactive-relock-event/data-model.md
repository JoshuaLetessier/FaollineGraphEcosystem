# Phase 1 — Data Model: re-lock event

Additive to `ReactiveEvaluator` (`Faolline.GraphStandard`). No new types; graphcore/gameflow untouched.

## ReactiveEvaluator — added

| Member | Kind | Description |
|--------|------|-------------|
| `OnNodeLocked` | NEW `public event Action<string>` | Raised when a node enters `ReactiveNodeState.Locked` — the counterpart of `OnNodeAvailable`/`OnNodeCompleted`. |
| `EmitFor(string, ReactiveNodeState)` | changed body (private) | Adds `else if (state == Locked) OnNodeLocked?.Invoke(nodeId);`. No signature change; no change to the Available/Completed branches. |

## Emission semantics (unchanged choke point)

```
EmitFor(nodeId, state):
  if Available  → OnNodeAvailable(nodeId)
  else if Completed → OnNodeCompleted(nodeId)
  else if Locked → OnNodeLocked(nodeId)        // NEW
```

`EmitFor` is called by `Start()` for every node (initial emission) and by `Reevaluate()` only when
`!known || prev != state` (transition). So `OnNodeLocked` fires: once per initially-Locked node at `Start()`,
and on each Available/Completed→Locked transition during `Reevaluate()`.

## Validation / invariants

- **INV-1**: A k-of-N node Available, then completed-set drops below `k` + `Reevaluate` → `OnNodeLocked` fires
  once for it; `GetState` is `Locked`.
- **INV-2**: `Start()` raises `OnNodeLocked` for each initially-Locked node, and not for Available/Completed ones.
- **INV-3**: A `Reevaluate` that leaves a node's state unchanged raises no `OnNodeLocked` for it.
- **INV-4**: `OnNodeAvailable`/`OnNodeCompleted` and derivation are byte-for-byte unchanged; existing tests green.
- **INV-5**: graphcore/gameflow untouched; graphstandard `0.5.0 → 0.6.0`.
