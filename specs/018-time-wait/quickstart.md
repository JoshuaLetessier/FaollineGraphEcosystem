# Quickstart — P5 Time (host-fed wait)

## 1. Author: make a node pause for a duration

```csharp
node.WaitDuration = 2.0f;   // hold 2 seconds on entry; 0 (default) = no hold
```

## 2. Host: feed elapsed time

```csharp
var runner = new BaseRunner();
runner.OnWaitingForTime += (n, secs) => Debug.Log($"waiting {secs}s at {n.Id}");
runner.Start(graph, ctx, registry);
// ... graph is holding at the timed node ...

void Update() => runner.Tick(Time.deltaTime);   // advances when cumulative dt ≥ WaitDuration
```

## 3. Time is the host's to control (no extra API)

- **Pause**: stop calling `Tick` (or `Tick(0)`).
- **Slow-motion**: `runner.Tick(Time.deltaTime * 0.5f)`.
- **Fast-forward / skip**: `runner.Tick(999f)` — overshoot still advances.

## 4. Rules

- `WaitDuration = 0` ⇒ no hold (identical to before).
- `Proceed`/`ChooseById` are inert while time-waiting — only `Tick` advances it.
- If a node sets BOTH `WaitDuration` and `AwaitSignalName`, the **signal wins** (time ignored).
- Step-back into a timed node **restarts** its countdown.
- The runner owns no clock — it only consumes the seconds you feed it.

## 5. Verify

graphcore EditMode tests cover hold, tick-to-advance, overshoot, pause, default-no-wait, signal-precedence,
inert manual advance, and step-back re-arm — all headless.
