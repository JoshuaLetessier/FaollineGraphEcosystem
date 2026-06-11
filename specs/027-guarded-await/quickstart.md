# Quickstart — a re-armable, condition-gated await

Park a node on a signal and gate its resume on the world state. The signal can be raised anytime; it only
advances the flow when the gate passes, and is harmlessly ignored (re-armable) until then.

```csharp
// In-graph: the "leave the room" node only resumes on "exit" once 2 of 3 puzzles are done.
builder.AddStatement("room")
       .Await("exit")
       .ResumeWhen(CountAtLeast("completed", 2));   // a CollectionCountAtLeastCondition (graphstandard)

// The shell parks on "room". The player can hit the exit anytime:
driver.RaiseSignal("exit");   // ignored while < 2 solved (node stays parked) …
// … a puzzle records into "completed" …
driver.RaiseSignal("exit");   // now resumes — the flow leaves the room
```

No host glue: the "is the door open?" fact lives once, in the graph, as the await's resume gate. The completed-set
can be fed by the Reactive engine (`MarkCompleted`) or a flow `AddToCollectionAction` — the gate just reads the
context.

## Why re-arm (not consume)

Gating the node's *outgoing edge* would consume the signal first and leave you stuck on a false gate. A resume
gate instead **keeps the node parked** when the gate fails, so player input ("press the button") is naturally
retriable until the world is ready — the pattern behind locked doors, shop buys, craft, lobby-ready, and most
player→world interactions.

## No gate = unchanged

A node with no `ResumeConditions` resumes on the signal name alone, exactly as before.
