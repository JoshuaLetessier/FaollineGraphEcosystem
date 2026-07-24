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

### Theoretical stress test (2026-07-23): this was actually two problems

Before writing more code, the three candidate models below were stress-tested on paper against the
confirmed case above and the ecosystem's other nesting patterns. Result: **"signal scoping" was hiding
two distinct problems** that the original "two sub-graphs (or two instances of the same sub-graph)"
phrasing conflated without saying so.

| | Sibling-runner (the confirmed case above) | Re-entrant/nested sub-graph |
|---|---|---|
| Shape | two **independent** `BaseRunner` instances, no execution-tree relationship, sharing one `BaseContext` by DI choice | **one** runner's `_graphStack`, the same or a related sub-graph reached twice (nesting or replay) |
| Who knows the difference | only the caller (game code) — "this is tile Nord" is not derivable from graph structure | the runtime itself — parent/child is right there in `_graphStack` |
| Which candidate model can even apply | **(b) explicit channel only.** (a) implicit instance-scope and (c) hierarchical fall-through are BOTH defined over the graph execution tree — two sibling runners have no tree relationship for either to hook into. Scoping "by instance" here requires information only the caller has, which is (b) by another name. | (a)/(c) — natural extension of the existing `OpensScope` local-context overlay, which is already exactly "child gets its own scope, reads through to parent" |
| Status | **Resolved** (`SignalPayloadMatchesCondition` + `IResumeSignalAwareCondition`, 0.36.0) — and will PERMANENTLY need an explicit mechanism no matter what scoping model (if any) is ever built for the other problem | **Open, deliberately not fixed yet** — see below |

The re-entrant/nested case turned out to already have a live, undocumented bug to point at:
`BaseContext`'s local-context overlay (`OpensScope`) is a **flat overlay, not a stack** —
`BeginLocalContext` silently discards an already-open local context if a second `OpensScope` sub-graph
is reached while the first is still open (same context object, via `OpensScope`/`InheritParentContext`
hops). Zero test coverage, zero validator check, zero dogfood report before this session — confirmed
genuinely unhit in practice so far.

**Decision: don't build the real fix (a proper scope stack) now.** The ecosystem is headed toward a
non-linear/parallel execution engine (a Behavior Tree, per the execution-paradigms direction) whose
scoping needs are structurally different from this narrow case — real concurrent branches (a `Parallel`
composite) need a scope **tree**, not a LIFO stack, and BT memory is typically per-node, not
per-subtree like `OpensScope`. Fixing the flat overlay now, shaped only around Linear's rare nested-
`OpensScope` case, risks freezing something a BT engine would have to redesign anyway — the exact
"commits to a model expensive to change later" trap this file already warns about for signal scoping
itself. **Shipped instead (graphcore 0.36.1), cheap and non-committal:** `GraphValidator` now warns at
authoring time when a graph nests `OpensScope` sub-graphs (through any depth of
`InheritParentContext` hops), and the gap is documented directly on
`SubGraphNodeData.OpensScope`. The real scope-stack design is parked until a non-linear engine is
actually being designed — then design it ONCE as shared substrate (covering both that engine's needs
and a retrofit of `OpensScope`), not twice.

### Why the remaining (Signal-scoping-proper) piece is still deferred

The sibling-runner problem is closed for good (payload+condition is the permanent answer regardless of
scoping). What's left un-decided is only whether SIGNALS (not variables) should ever gain a scoping
mechanism at all for the nested/re-entrant case — and per the table above, that would piggyback on
whatever scope-stack substrate eventually gets built for a non-linear engine, not be designed standalone
for signals today. Committing to a model now (graph-scoped? explicit named channels? hierarchical
fall-through?) before that substrate exists freezes semantics that are expensive to change later.

### What we need before building the real scope-stack substrate

- A non-linear/parallel execution engine (Behavior Tree or similar) actually in design, so the stack's
  shape (tree vs. LIFO, per-node vs. per-subtree memory) is derived from real requirements instead of
  guessed from Linear's one rare, currently-unused nested-`OpensScope` case.
- At that point, re-evaluate whether Signals should plug into it too (they currently don't participate
  in the local-context overlay at all — a deliberate design choice, not an oversight, since signals are
  transient/global by design today).

### Non-goals for now

- No API surface added speculatively.
- Consumers that need per-instance signal disambiguation today use `SignalPayloadMatchesCondition`
  (permanent, not a stopgap, for the sibling-runner case) or the existing `InheritParentContext` /
  `OpensScope` boundary (for the nested-runner case, now with a validator warning against the one way
  it currently breaks).

---

## Mixed scene-loader flows (Build Settings + Addressables in one flow)

**Status:** closed — not a real gap. Dropdown ergonomics SHIPPED (graphgameflow 0.16.0).
**Origin:** design discussion, 2026-07-24.

### The observation

`GameFlowContext.SceneLoader` is a single field (`GameFlowContext.cs:20`) — one active `ISceneLoader` for the
whole context. `LoadSceneAction.Execute` and `UnloadSceneAction` both resolve it fresh from the running
context (`LoadSceneAction.cs:47`), so every scene action in a flow is interpreted the same way: all
Build-Settings scene names, or all Addressable keys, never a per-action choice. There's no way today to have
one `LoadSceneAction` in a flow resolve as a Build-Settings name while another resolves as an Addressable key.

### Why this stopped mattering (user, 2026-07-24)

The one place a real project actually needs both loading mechanisms at once is the mandatory single
Addressables bootstrap scene (Addressables requires one Build-Settings-resolved scene to initialize itself
before any Addressable content can load). But that boot scene is never a `LoadSceneAction`/`UnloadSceneAction`
target in the first place — it's the scene that starts everything (including the `GraphFlowDriver` itself),
not one this ecosystem's own graphs load or unload. So the "mixed flow" case this section was written for
doesn't actually occur in practice: a project either drives its whole flow off Build Settings, or off
Addressables past the boot scene — never both from inside the same graph. Per-action/hybrid loader
resolution is therefore **not just deferred, it's a non-problem** — closing this instead of leaving it parked.

### What already works around it (if a real case ever shows up)

Two separate `GraphFlowDriver`/`GameFlowContext` instances — one per sub-system (e.g. core scenes on Build
Settings, downloadable chapters on Addressables) — each with its own loader. This is two flows, not a mix
within one flow, but it's the already-working path and needs zero lib changes.

### What shipped instead (ergonomics, not the mix)

`SceneNameFieldDrawer` (graphgameflow 0.16.0): a "Build Settings / Addressable" toolbar on both
`LoadSceneAction`/`UnloadSceneAction` inspectors. In Addressable mode, a dropdown lists registered
Addressables-group scene addresses, plus a "Mark as Addressable" button (mirrors "Add to Build Settings") to
promote a plain project scene to an Addressable entry in one click. Gated behind a `FAOLLINE_ADDRESSABLES`
Version Define so the core package adds no hard dependency when Addressables isn't installed.

### Non-goals

- No per-action or hybrid-fallback loader resolution — closed as a non-problem, not deferred.
- No change to `GameFlowContext.SceneLoader` single-field model.
