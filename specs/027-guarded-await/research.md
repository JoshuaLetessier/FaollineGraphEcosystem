# Phase 0 — Research: guarded await

## R1 — `ResumeConditions` mirrors `EntryConditions`

**Decision**: Add `[SerializeField] private List<BaseCondition> _resumeConditions = new List<BaseCondition>();`
to `BaseNodeData` with `public List<BaseCondition> ResumeConditions => _resumeConditions;` — identical to the
existing `EntryConditions` member.

**Rationale**: Reuses a proven, serialized, editor-friendly pattern; the default empty list means deserialized
pre-0.7.0 assets carry no gate and behave exactly as before (append-only). Symmetry with `EntryConditions`
keeps the mental model simple (entry-gate vs resume-gate).

**Alternatives**: *a single condition, not a list* — rejected: entry conditions are a list (AND); symmetry and
composability win. *a new condition subtype* — rejected: universal `BaseCondition` already fits.

## R2 — Gate in `ResumeIfAwaiting`, ignore-not-consume (re-arm)

**Decision**: `ResumeIfAwaiting(name)` resumes only if `node.AwaitSignalName == name` **and**
`ResumeConditionsPass(node)`; otherwise it returns without changing state — the node stays `WaitingForSignal`.

**Rationale**: This is the capability's whole point and its differentiator from the existing alternative (gating
the node's *outgoing edge*): gating the edge consumes the signal first, so a false gate exits the await with no
valid edge → stuck. Gating the *resume* leaves the node parked and **re-armable**: the actor can raise the
signal again once the world is ready. The signal is still recorded on the context (the existing
`_context.RaiseSignal` runs before `ResumeIfAwaiting`), so a resume condition may even read the just-raised
payload; only the *resume* is gated.

**Alternatives**: *latch a failing raise to auto-fire when the gate later passes* — deferred (YAGNI; the
consumer's natural usage is "raise only when ready", and re-arm covers it). *consume-and-route via edge
conditions* — already possible, but stuck-on-false is the wrong semantics for player-retriable input.

## R3 — Host override stays ungated

**Decision**: Resume conditions gate **signal-driven** resume (`RaiseSignal → ResumeIfAwaiting`) only. A direct
host advance (`Advance`, forced GoTo) is unchanged and not gated.

**Rationale**: `GoTo`/forced advance already "bypass condition evaluation" by contract — an explicit host
override. Keeping it ungated preserves that escape hatch and avoids surprising existing callers.

## R4 — Builder `ResumeWhen` mirrors `When`

**Decision**: `GraphNodeBuilder.ResumeWhen(params BaseCondition[] conditions)` appends non-null conditions to
`Node.ResumeConditions`, returning `this` — identical to `When` (which appends to `EntryConditions`).

**Rationale**: The capability must be reachable through the code-first authoring path consumers use; mirroring
`When` keeps the fluent surface consistent (`Await(name).ResumeWhen(cond)`).
