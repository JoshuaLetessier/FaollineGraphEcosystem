# Quickstart — P1 Signals

How a **graph author** makes a node wait for an event, and how a **host integrator** raises events,
subscribes, and reads payloads. (graphcore 0.4.0; all headless / EditMode-testable.)

## 1. Author: make a node hold until a signal

Set the await flag on any node — no new node type needed:

```csharp
// e.g. a statement node that should pause until the host says the door opened
node.AwaitSignalName = "doorOpened";   // "" (default) = node does not hold
```

When the runner enters this node it runs the node's entry-conditions, enter-actions, and executor as
usual, then **holds**: `State == RunnerState.WaitingForSignal` and `OnWaitingForSignal(node, "doorOpened")`
fires. `Proceed()` / `ChooseById()` do nothing here — only the matching signal advances it.

## 2. Integrator: drive a held graph with a signal

```csharp
var runner = new BaseRunner();
runner.OnWaitingForSignal += (node, sig) => Debug.Log($"waiting for '{sig}'");
runner.Start(graph, context, registry);

// ... the graph is now holding at the await node ...

runner.RaiseSignal("doorOpened");              // resumes: delivers + advances
// or with a payload:
runner.RaiseSignal("itemCollected", "key");    // string payload
```

`BaseRunner.RaiseSignal` first delivers to subscribers, then — if the current node awaits that exact
name — advances using the normal exit-actions + edge-selection rules. A non-matching name only delivers
(the graph keeps waiting).

## 3. Subscribe and read payloads (no runner required)

Pub/sub and payload reads work on the context alone (US1/US3), independent of any await:

```csharp
context.OnSignal("itemCollected", args =>
{
    if (args.HasPayload)
        Debug.Log($"collected: {args.GetPayload<string>()}");
    else
        Debug.Log("collected (no payload)");
});

context.RaiseSignal("itemCollected", "key");   // delivery only (does NOT resume a held graph)

if (context.TryGetLastSignal("itemCollected", out var last) && last.HasPayload)
    var what = last.GetPayload<string>();       // "key"
```

A condition/action can branch on the last payload:

```csharp
public override bool Evaluate(BaseContext ctx)
    => ctx.TryGetLastSignal("itemCollected", out var a)
       && a.HasPayload && a.GetPayload<string>() == "key";
```

## 4. Key rules

- **Transient (v1)**: a signal raised while nothing awaits it is delivered to current subscribers and
  **forgotten** — it will not satisfy a future await. (State-based "it already happened" gating is the
  Reactive engine's job, roadmap P3.)
- **Match on name only**: the payload is data you read, not part of what satisfies an await.
- **One scalar payload** (bool/int/float/string) or none; `HasPayload` distinguishes the two.
- **Runner vs context**: raise on the **runner** to advance a held graph; raise on the **context** for
  pure pub/sub.
- **No signals used ⇒ nothing changes**: empty `AwaitSignalName` everywhere + no `RaiseSignal` calls =
  identical to graphcore 0.3.0.

## 5. Verify in graphTest

The sandbox `com.faolline.graphTest` exercises the full surface end-to-end: an await node that holds, a
`RaiseSignal`-driven advance, broadcast to multiple subscribers, a payload-reading condition, the
no-subscriber no-op, and the back-compat path — all EditMode, editor closed.
