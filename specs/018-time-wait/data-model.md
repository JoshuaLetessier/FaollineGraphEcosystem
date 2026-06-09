# Phase 1 — Data Model: P5 Time

Append-only additions (graphcore 0.5.0 → 0.6.0). Existing signatures unchanged.

## `BaseNodeData` — `Runtime/Nodes/BaseNodeData.cs`

- New `[SerializeField] private float _waitDuration;` + `public float WaitDuration { get; set; }`
  (default 0). N1 — `0` (or negative) ⇒ the node does not hold; pre-existing assets deserialize to 0 ⇒
  behaviour identical to 0.5.0. N2 — append-only.

## `RunnerState` — `Runtime/Execution/RunnerState.cs`

- Append `WaitingForTime = 5` (after `WaitingForSignal = 4`). XML-documented: held on a `WaitDuration`;
  `Proceed`/`ChooseById` are no-ops; only `Tick` advances it.

## `BaseRunner` — `Runtime/Execution/BaseRunner.cs`

- New `private float _waitRemaining;`.
- New `public event Action<BaseNodeData, float> OnWaitingForTime;` (node + duration).
- New `public void Tick(float deltaSeconds)`:
  - if `_state != WaitingForTime` → return (no-op).
  - if `deltaSeconds <= 0f` → return (pause / no progress).
  - `_waitRemaining -= deltaSeconds;` if `_waitRemaining <= 0f` → `ExitAndAdvance()`.
- `EnterCurrentNode` — after the existing await-signal branch, before the `NodeReady`/`OnNodeCompleted`
  lines, insert:
  - if `node.WaitDuration > 0f`: `_state = WaitingForTime; _waitRemaining = node.WaitDuration;`
    `OnWaitingForTime?.Invoke(node, node.WaitDuration); return;`

### State-transition table

| From | Trigger | To | Effect |
|------|---------|----|--------|
| entering a node, `WaitDuration > 0`, no `AwaitSignalName` | — | `WaitingForTime` | `OnNodeEntered` + `OnWaitingForTime`; no `OnNodeCompleted` |
| `WaitingForTime` | `Tick(dt>0)` accumulating ≥ duration | advances → next node | `ExitAndAdvance` (existing exit-actions + edge selection) |
| `WaitingForTime` | `Tick(dt>0)` accumulating < duration | `WaitingForTime` | decrement only |
| `WaitingForTime` | `Tick(dt<=0)` | `WaitingForTime` | no-op (pause) |
| `WaitingForTime` | `Proceed`/`ChooseById` | `WaitingForTime` | no-op |
| any non-WaitingForTime | `Tick` | unchanged | no-op |

**Invariants**
- W1 — host-fed only: a time-wait advances solely via `Tick` (FR-002/FR-003).
- W2 — overshoot satisfies: a single large tick (≥ remaining) advances (FR US1.4).
- W3 — signal precedence: `AwaitSignalName` non-empty ⇒ the signal branch wins; `WaitDuration` ignored (FR-006).
- W4 — re-arm: re-entering a timed node (step-back) restarts the full countdown (FR-008).
- W5 — back-compat: `WaitDuration = 0` everywhere + no `Tick` ⇒ identical to 0.5.0 (FR-009).
