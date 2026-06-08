# Phase 1 — Public API Contract: P1 Signals

Authoritative new public surface for graphcore 0.4.0 and the testable invariants each member must honor.
Everything here is **additive**; no existing member changes.

## `Faolline.GraphCore.SignalArgs` (readonly struct)

```csharp
public readonly struct SignalArgs
{
    public string Name { get; }
    public bool   HasPayload { get; }
    public object PayloadBoxed { get; }   // boxed bool/int/float/string, or null
    public T GetPayload<T>();             // typed access
}
```

- INV-SA1: `Name` is the raised name (non-empty for any delivered signal).
- INV-SA2: `HasPayload == false` ⇔ `PayloadBoxed == null` ⇔ raised via `RaiseSignal(name)`.
- INV-SA3: `GetPayload<T>()` returns the value when `T` matches the stored scalar type; throws
  `InvalidOperationException` if `!HasPayload`, `InvalidCastException` on type mismatch.

## `Faolline.GraphCore.BaseContext` (additions)

```csharp
public void RaiseSignal(string name);
public void RaiseSignal<T>(string name, T payload);   // T ∈ {bool,int,float,string}
public void OnSignal(string name, Action<SignalArgs> handler);
public void OffSignal(string name, Action<SignalArgs> handler);
public bool TryGetLastSignal(string name, out SignalArgs args);
```

- INV-C1: `RaiseSignal` delivers `SignalArgs` to every current subscriber of `name` (broadcast),
  iterating a snapshot (re-entrant safe).
- INV-C2: `RaiseSignal` with zero subscribers is a no-op that still updates the last-signal store; never
  throws.
- INV-C3: `RaiseSignal<T>` with `T ∉ {bool,int,float,string}` throws `ArgumentException` (parity with
  `Set<T>`).
- INV-C4: null/empty `name` on any of the five methods logs `[GraphCore]` and no-ops.
- INV-C5: `TryGetLastSignal` returns the most recent `SignalArgs` for `name`, else `false` + `default`.
- INV-C6: Signals never enter `_params`; `GetAllParameters()`, `DeepClone()`, `CopyValuesFrom()` are
  unaffected (no signal data in saves/snapshots). Subscribers are not cloned.

## `Faolline.GraphCore.BaseNodeData` (addition)

```csharp
public string AwaitSignalName { get; set; }   // default ""; "" ⇒ not awaiting
```

- INV-N1: setter coerces null → `""`.
- INV-N2: default `""` ⇒ node behaves exactly as 0.3.0 (no hold).

## `Faolline.GraphCore.RunnerState` (addition)

```csharp
WaitingForSignal = 4
```

- INV-R1: appended value; `Idle/NodeReady/Paused/Ended` keep their numbers.

## `Faolline.GraphCore.BaseRunner` (additions)

```csharp
public event Action<BaseNodeData, string> OnWaitingForSignal;
public void RaiseSignal(string name);
public void RaiseSignal<T>(string name, T payload);
```

- INV-B1 (hold): entering a node whose `AwaitSignalName` is non-empty sets `State == WaitingForSignal`,
  fires `OnWaitingForSignal(node, name)`, and does NOT fire `OnNodeCompleted`.
- INV-B2 (resume): `BaseRunner.RaiseSignal(name)` delivers via the active context, then — iff
  `State == WaitingForSignal` and the current node's `AwaitSignalName == name` — advances exactly as
  `Proceed` would (same exit-actions + edge selection).
- INV-B3 (mismatch): a non-matching name delivers to subscribers but leaves `State == WaitingForSignal`.
- INV-B4 (inert manual drive): `Proceed`/`ChooseById` are no-ops while `WaitingForSignal`.
- INV-B5 (back-compat): with no awaiting nodes and no `RaiseSignal` calls, behaviour is byte-for-byte 0.3.0.
- INV-B6 (step-back): returning to an awaiting node via `GoBack`/`GoBackToCheckpoint` re-arms the wait.
- INV-B7 (layering): `BaseContext.RaiseSignal` performs delivery only; resuming a held graph requires
  `BaseRunner.RaiseSignal`.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1 #1 deliver w/ payload | INV-C1, INV-SA2 |
| US1 #2 no subscriber no-op | INV-C2 |
| US1 #3 broadcast | INV-C1 |
| US1 #4 no-payload distinct | INV-SA2, INV-C5 |
| US2 #1 hold | INV-B1 |
| US2 #2 resume via signal | INV-B2 |
| US2 #3 mismatch keeps waiting | INV-B3 |
| US2 #4 no awaits ⇒ identical | INV-B5, INV-N2 |
| US3 #1 read payload | INV-C5, INV-SA3 |
| US3 #2 detect absence | INV-SA2, INV-C5 |
| FR-012 step-back re-arms | INV-B6 |
| SC-002 suite green unmodified | INV-B5, INV-C6, INV-R1, INV-N2 |
