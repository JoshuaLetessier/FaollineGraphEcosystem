# Phase 1 — Data Model: P1 Signals

Exact type changes, invariants, and state transitions. All changes are **append-only** (semver MINOR,
graphcore 0.3.0 → 0.4.0). Existing public signatures are unchanged.

## New type: `SignalArgs` (public readonly struct) — `Runtime/Signals/SignalArgs.cs`

| Member | Type | Notes |
|--------|------|-------|
| `Name` | `string` | The raised signal's name. Never null/empty for a delivered signal. |
| `HasPayload` | `bool` | `true` when a scalar payload accompanied the raise. |
| `PayloadBoxed` | `object` | The boxed scalar (bool/int/float/string) or `null` when `HasPayload == false`. |
| `GetPayload<T>()` | `T` | Returns the payload typed. Throws `InvalidCastException` on type mismatch and `InvalidOperationException` when `HasPayload == false`. |

- Constructed only by `BaseContext` (internal ctor). Immutable. Supported payload `T`: `bool`, `int`,
  `float`, `string` (same set as `BaseContext`).

## `BaseContext` — signal channel (additions only)

New private fields:
- `Dictionary<string, List<Action<SignalArgs>>> _signalSubs` — subscribers per signal name.
- `Dictionary<string, SignalArgs> _lastSignals` — last `SignalArgs` delivered per name (transient).

New public methods:
- `void RaiseSignal(string name)` — raise with no payload.
- `void RaiseSignal<T>(string name, T payload)` — raise with a scalar payload (`T` validated to
  bool/int/float/string; else `ArgumentException`, same as `Set<T>`).
- `void OnSignal(string name, Action<SignalArgs> handler)` — subscribe.
- `void OffSignal(string name, Action<SignalArgs> handler)` — unsubscribe.
- `bool TryGetLastSignal(string name, out SignalArgs args)` — read last delivery; `false` + default when
  none seen.

**Invariants**
- C1 — **Excluded from typed state**: `_signalSubs`/`_lastSignals` are NOT in `_params`. `GetAllParameters`,
  `DeepClone`, `CopyValuesFrom`, `InitFromGraph`, the local-context overlay — **all unchanged**; signals
  never appear in saves, snapshots, or `GetAllParameters`.
- C2 — **No-op on no subscriber**: `RaiseSignal` on a name with zero subscribers updates `_lastSignals`
  and returns; never throws (FR-003, SC-005).
- C3 — **Broadcast + re-entrant safe**: all current subscribers are invoked over a **snapshot copy** of
  the list (mirrors `FireSubscribers`), so subscribe/unsubscribe during delivery does not corrupt
  iteration (FR-002, edge cases).
- C4 — **Null/empty name**: `RaiseSignal`/`OnSignal`/`OffSignal` with null-or-empty name log a
  `[GraphCore]` warning and no-op (FR-010).
- C5 — **No-payload distinct**: a `RaiseSignal(name)` produces `SignalArgs` with `HasPayload == false`;
  a `RaiseSignal<T>` produces `HasPayload == true` (FR-007).
- C6 — **Subscribers not cloned**: `DeepClone` does not copy `_signalSubs` (same policy as `_subs`).

## `BaseNodeData` — await flag (addition only) — `Runtime/Nodes/BaseNodeData.cs`

- New serialized field `[SerializeField] private string _awaitSignal = string.Empty;`
- New property `string AwaitSignalName { get => _awaitSignal; set => _awaitSignal = value ?? string.Empty; }`

**Invariants**
- N1 — Default `""` ⇒ node is **not** awaiting; pre-existing assets deserialize with empty ⇒ behaviour
  identical to 0.3.0 (FR-008).
- N2 — Append-only: field is added after existing fields; no rename/reorder/removal.

## `RunnerState` — new state (append) — `Runtime/Execution/RunnerState.cs`

- Add `WaitingForSignal = 4` (after `Ended = 3`). XML-documented: "The current node declared
  `AwaitSignalName`; the runner has entered it and is holding until that signal is raised. `Proceed`/
  `ChooseById` are no-ops in this state; only a matching `RaiseSignal` advances."

**Invariants**
- S1 — Appended numeric value; existing values unchanged (append-only).
- S2 — `Proceed`/`ChooseById` keep their `if (_state != NodeReady) return;` guard ⇒ they are inert while
  `WaitingForSignal` (no signature/behaviour change to those methods).

## `BaseRunner` — await/resume (additions + one guarded branch) — `Runtime/Execution/BaseRunner.cs`

New public surface:
- `event Action<BaseNodeData, string> OnWaitingForSignal` — fired when an awaiting node is entered
  (node + awaited signal name).
- `void RaiseSignal(string name)` and `void RaiseSignal<T>(string name, T payload)` — deliver to the
  active context, then resume if appropriate.

Changed internal flow (existing public signatures untouched):
- `EnterCurrentNode`: after `OnNodeEntered?.Invoke(node)`, branch:
  - if `!string.IsNullOrEmpty(node.AwaitSignalName)`: `_state = WaitingForSignal;`
    `OnWaitingForSignal?.Invoke(node, node.AwaitSignalName);` **return** (skip `OnNodeCompleted`).
  - else: `_state = NodeReady;` `OnNodeCompleted?.Invoke(node);` (the `NodeReady` assignment is new but
    idempotent for all existing flows — see R3).
- `RaiseSignal(...)`: if name null/empty → `[GraphCore]` warning + return. Else
  `_context.RaiseSignal(name[, payload])` (delivery + `_lastSignals`). Then if
  `_state == WaitingForSignal` and `CurrentNode?.AwaitSignalName == name` → `ExitAndAdvance()` (resume).

### Runner state-transition table

| From | Trigger | To | Effect |
|------|---------|----|--------|
| `NodeReady` (entering a node with empty `AwaitSignalName`) | — | `NodeReady` | `OnNodeEntered` + `OnNodeCompleted` (unchanged) |
| entering a node with non-empty `AwaitSignalName` | — | `WaitingForSignal` | `OnNodeEntered` + `OnWaitingForSignal`; **no** `OnNodeCompleted` |
| `WaitingForSignal` | `RaiseSignal(matching name)` | advances → next node's `NodeReady`/`WaitingForSignal`/`Ended` | deliver to subscribers, then `ExitAndAdvance` (existing exit-actions + edge selection) |
| `WaitingForSignal` | `RaiseSignal(non-matching name)` | `WaitingForSignal` | deliver to subscribers only; **no** advance (FR-006) |
| `WaitingForSignal` | `Proceed`/`ChooseById` | `WaitingForSignal` | no-op (S2) |
| any | `RaiseSignal` (any name) | unchanged | always delivers to subscribers (US1 independent of await) |

**Invariants**
- B1 — **Resume uses existing edge selection**: resume calls the same `ExitAndAdvance` path as `Proceed`,
  so conditional edges / `SelectEdge` behave identically (FR-005).
- B2 — **Back-compat**: with all `AwaitSignalName` empty and no `RaiseSignal` calls, every code path is
  the 0.3.0 path; the added `_state = NodeReady` before `OnNodeCompleted` is a no-op (FR-008, SC-003).
- B3 — **Step-back re-arms**: `GoBack`/`GoBackToCheckpoint` → `RestoreEntry` → `EnterCurrentNode` re-runs
  the await branch for an awaiting node ⇒ `WaitingForSignal` re-entered (FR-012). No snapshot change.
- B4 — **Delivery precedes resume**: subscribers see the signal before the graph advances.
- B5 — **Context-level raise does not resume**: `BaseContext.RaiseSignal` performs delivery only (US1/US3);
  resuming an awaiting graph requires `BaseRunner.RaiseSignal` (documented in quickstart/contract).
