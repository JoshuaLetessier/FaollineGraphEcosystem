# TODO — deferred design work

Larger design questions parked here on purpose: they change core semantics and need **real usage by
several consumers** before a direction is locked in. Not bugs, not small adds — do not "just implement" these
without that feedback.

---

## Signal scoping (contexts / channels)

**Status:** deferred — needs multi-user feedback before choosing a model.
**Origin:** Cryptique 4-region dogfood (2026-06-20), finding #5.

### The observation

Signals (`BaseContext.RaiseSignal` / `OnSignal` / `HasSignalBeenRaised`) are **global to the context**. There
is no scoping by graph, by sub-graph instance, or by channel. For a single flow this is fine and simple. But:

- Two sub-graphs (or two instances of the same sub-graph) that raise/await the **same signal name** hear each
  other — there is no isolation.
- A signal raised deep in one branch is visible to every subscriber on the same context, whatever its intent.

### What already mitigates it (do NOT mistake for a solution)

- `SubGraphNodeData.InheritParentContext` / `OpensScope` already give **coarse** isolation: a fresh-context
  sub-graph does not share signals with its parent at all (that's a boundary, not a scope). The
  `GraphValidator` even warns when that boundary would deadlock an await (graphcore 0.24.0).
- `RaiseSignal<T>(name, payload)` carries a typed payload — an embryo of a "channel", but not addressing.
- **`SignalPayloadMatchesCondition` (graphcore 0.36.0)** — a `BaseCondition` for
  `BaseNodeData.ResumeConditions` that gates a resume on the raised signal's last string payload matching
  an expected value, so a homonymous raise meant for a different instance is ignored (the node "stays
  parked and re-armable", per `BaseRunner.ResumeConditions` semantics) instead of falsely resuming. Also
  handles a node awaiting MORE than one signal name (e.g. a completion signal plus a failure signal on
  `AwaitSignalNamesExtra`): put one instance per awaited name — each implements
  `IResumeSignalAwareCondition` and abstains (passes) on any raised name that isn't its own `Signal`, so
  `BaseRunner`'s AND-across-`ResumeConditions` composes them as the intended OR instead of one condition
  vetoing a resume it had no opinion on. `MatchMode` (`Exact`/`StartsWith`) covers payload formats like
  the `"{sceneName}: {reason}"` failure signals from `AsyncSceneLoader`/`AddressablesSceneLoader`.

None of these give *fine-grained* scoping (per-instance / named channel within a shared context) as a
general mechanism — they are targeted workarounds for the payload-carrying case, not a scoping model.

### Confirmed real-world case (2026-07-23, consumer report)

A concrete repro was worked through for the proximity-streaming pattern (world cut into tiles, additive
loaded/unloaded by player proximity — see this project's own streaming guide, "Streaming par proximité"):
several tile/zone flows, each its own `GraphFlowDriver`/`BaseRunner` instance, sharing one `GameFlowContext`
(the normal DI setup so every tile can still read shared state — inventory, quest progress). All tiles go
through one shared `AsyncSceneLoader` (needed for its FIFO queue and "preload in the direction of travel"
behavior), which exposes exactly one `LoadCompletedSignal` per instance. Two tiles parked on that same
signal name — tile B resumes the instant tile A's unrelated load completes, believing its own scene is
ready when it hasn't even started loading. Verified against the actual code, not just theorized:
`BaseRunner.ResumeIfAwaiting` (`Contains(node.AwaitSignalNames, name)`) matches on name only, and
`BaseContext.OnSignal` subscriptions are context-scoped, so every runner sharing that context receives the
broadcast regardless of which runner's `RaiseSignal` triggered it.

This is exactly the case the "≥2 independent consumers" bar in this file was waiting for — it is not a
contrived demo, and the two documented mitigations (`OpensScope`, name-namespacing by convention) both
fail it specifically: `OpensScope` would cut the tile off from the shared state it needs to read, and
namespacing by convention doesn't work when tile names are assigned procedurally at runtime, not authored
up front. `SignalPayloadMatchesCondition` (above) closes this specific case without committing to a
scoping model — it's a point fix, not evidence the deferred design work above is no longer needed.

### Why it is deferred (not just "do it")

Scoping touches `BaseContext`, the runner's await subscription, and the meaning of `OnSignal`/`AwaitSignalName`
across the whole ecosystem. Committing to a model (graph-scoped? instance-scoped? explicit named channels?
hierarchical fall-through?) freezes semantics that are expensive to change later. It belongs with the
**execution-paradigms direction** (splitting the graphcore substrate from pluggable engines), not as a bolt-on.

### What we need before choosing

- **≥2 independent consumers** hitting the global-signal limitation in real games (not a contrived demo), so
  the *shape* of the need is observed rather than guessed.
- A concrete case where the coarse `InheritParentContext` / `OpensScope` boundary is genuinely insufficient.
- Then evaluate models: (a) implicit graph/instance scope, (b) explicit named channels on `RaiseSignal`,
  (c) hierarchical scopes with fall-through — against migration cost and back-compat.

### Non-goals for now

- No API surface added speculatively.
- Consumers that need isolation today use the existing `InheritParentContext` / `OpensScope` boundary, or
  disambiguate signal **names** (namespacing by convention, e.g. `zone1/doorOpened`).
