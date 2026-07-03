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

Neither gives *fine-grained* scoping (per-instance / named channel within a shared context).

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
