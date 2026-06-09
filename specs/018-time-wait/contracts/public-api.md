# Phase 1 — Public API Contract: P5 Time

Additive public surface for graphcore 0.6.0. Everything else unchanged.

## `Faolline.GraphCore.BaseNodeData`

```csharp
public float WaitDuration { get; set; }   // seconds; default 0; 0/negative ⇒ no hold
```

## `Faolline.GraphCore.RunnerState`

```csharp
WaitingForTime = 5
```

## `Faolline.GraphCore.BaseRunner`

```csharp
public event Action<BaseNodeData, float> OnWaitingForTime;   // node + duration, on entering a timed node
public void Tick(float deltaSeconds);                        // host-fed elapsed time
```

## Invariants

- INV-1 (hold): entering a node with `WaitDuration > 0` (and no `AwaitSignalName`) sets
  `State == WaitingForTime`, fires `OnWaitingForTime(node, duration)`, and does NOT fire `OnNodeCompleted`.
- INV-2 (advance): `Tick` with positive dt decrements the remaining time; when it reaches ≤ 0 the runner
  advances exactly as `Proceed` would (exit-actions + edge selection).
- INV-3 (overshoot): a single `Tick(dt ≥ remaining)` advances.
- INV-4 (pause): `Tick(dt ≤ 0)` never advances.
- INV-5 (no-op): `Tick` while not `WaitingForTime` does nothing.
- INV-6 (signal precedence): a node with both `AwaitSignalName` and `WaitDuration` waits on the signal.
- INV-7 (inert manual): `Proceed`/`ChooseById` are no-ops while `WaitingForTime`.
- INV-8 (re-arm): step-back into a timed node restarts the full countdown.
- INV-9 (back-compat): `WaitDuration = 0` + no `Tick` ⇒ byte-for-byte 0.5.0 behaviour.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1.1 hold | INV-1 |
| US1.2 tick-to-advance | INV-2 |
| US1.3 no-wait identical | INV-9 |
| US1.4 overshoot | INV-3 |
| US2.1 pause | INV-4 |
| US2.2 fractional cumulative | INV-2 |
| Edge: both set ⇒ signal | INV-6 |
| Edge: step-back re-arm | INV-8 |
| Edge: inert Proceed | INV-7 |
| SC-002 suite green | INV-9 |
