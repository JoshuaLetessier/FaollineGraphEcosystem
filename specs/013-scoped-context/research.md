# Phase 0 Research: Global & Local Execution Contexts

All decisions target `com.faolline.graphcore` and are constrained by the GraphCore Constitution v1.2.0
(Foundation Stability NON-NEGOTIABLE). There were no open `[NEEDS CLARIFICATION]` markers entering this
phase — the spec resolved the global-write question during specification.

## R1 — Two flat contexts, not a scope stack

**Decision**: Model the feature as exactly **two value buckets** — one persistent *global* and one
transient *local* — rather than a root scope plus an arbitrary-depth stack of local scopes.

**Rationale**: The author confirmed scope-opening sub-graphs (e.g. "scenes") are **always sequential,
never nested**. With no nesting there is never more than one live local level, so a stack, fall-through
depth > 1, and `Push/Pop` semantics are unused machinery. Principle V (YAGNI): the simplest model that
satisfies the spec wins. Generalising to a stack later is itself a backward-compatible MINOR change.

**Alternatives considered**: (a) Scope **stack** with cascade reads (the original pre-spec handoff) —
rejected as unnecessary given no nesting; strictly more code and more public surface to freeze forever on
an append-only foundation. (b) Keeping a single context and clearing-by-prefix — rejected: prefix scanning
is O(n) per boundary and bakes a string convention into the core (Principle II tension).

## R2 — Overlay inside `BaseContext`, not a new `ScopedContext` subclass

**Decision**: Add the local overlay directly to `BaseContext` as an optional, nullable second bucket
(`_local`) plus an active flag. No new `ScopedContext` type.

**Rationale**: The runner threads a single `BaseContext` (`_context`) through every frame; if scoping
lived in a subclass the runner would need `is ScopedContext` checks and a way to construct the right
subtype for arbitrary downstream contexts — impossible generically. Putting the (universal) capability in
`BaseContext` means every context, including downstream typed subclasses, gets it for free, and the runner
calls `BeginLocalContext`/`EndLocalContext` unconditionally on the context it already holds. When no scope
is opened the overlay is `null` and every method behaves exactly as today (back-compat, zero alloc).

**Alternatives considered**: (a) New `ScopedContext : BaseContext` — rejected (runner type-coupling, breaks
downstream typed-context subclasses that can't also be `ScopedContext`). (b) Make `Set/Get` `virtual` so a
subclass routes — rejected: the routing is universal, belongs in the base, and changing methods to virtual
adds dispatch with no benefit here. Note this side-steps the previously-noted "`Set/Get` are not virtual"
blocker — we no longer need them virtual because the routing is in the base class itself.

## R3 — Resolve-and-write routing; no `ParameterData` change

**Decision**: A variable's "declared home" is operationalised as **the bucket it currently lives in**:
- **Read**: local (if active and key present) → else global.
- **Write**: local if active and key already in local → else global if key in global → else local (when a
  scope is active) → else global (no scope). I.e. *write back to where the value resolves; undeclared keys
  default to local while scoped, global otherwise.*

Globals are populated as today (the host/root graph's parameters seed the global bucket at start, or
runtime writes land there pre-scope). A scope opening seeds the **local** bucket from the scope-opening
sub-graph's own parameters. No new field on `ParameterData`.

**Rationale**: This satisfies every spec acceptance scenario (a global-declared key already lives in
global → its in-scope write routes global and persists, FR-006; an undeclared scratch key → local →
discarded, FR-004/US1) **without** adding a per-parameter `Global` flag or a separate global-key registry
to the frozen foundation. Read/write symmetry ("write where you read") is the least surprising rule and
makes the shadowing edge case (same key local + global) behave consistently — the local shadow captures
the write and is discarded on exit, re-exposing the global value.

**Alternatives considered**: (a) Add `bool Global` / `ParameterScope` to `ParameterData` + a global-keys
set in the context — rejected by YAGNI and Foundation Stability (a new data-contract field to freeze
forever, plus extra state) when bucket-residency already encodes the home. (b) Reserved `global.` key
prefix interpreted by the context — rejected during specification (bakes a convention into the pure core,
Principle II). (c) Writes always local; explicit "write to global" call — rejected: a *generic* key-only
action could not target a global, defeating reuse (Principle V).

## R4 — `OpensScope` flag on `SubGraphNodeData`; precedence over `InheritParentContext`

**Decision**: Add `bool OpensScope` (append-only, `[SerializeField] private bool _opensScope`, default
`false`) to `SubGraphNodeData`. When `true`, the sub-graph runs on the **parent context object** with a
fresh local overlay opened on entry. `OpensScope=true` takes precedence over `InheritParentContext` (a
scoped sub-graph inherently rides the parent context, then overlays a local level).

**Rationale**: The existing two behaviours are encoded by `InheritParentContext` (inherit vs. fresh-blank).
"Scoped" is a genuinely third behaviour; a separate append-only bool keeps the existing field's meaning
untouched (FR-007/FR-008) and defaults pre-existing nodes to their old behaviour (default false). Riding
the parent context object (not a fresh one) is what makes fall-through reads to global possible.

**Alternatives considered**: Replacing `InheritParentContext` with a 3-value enum — rejected: it would
change/rename an existing serialized field (Foundation Stability violation: fields are append-only). A new
independent bool is the only append-only-clean option.

## R5 — Runner lockstep: seed-on-enter, discard-on-end

**Decision**: In `BaseRunner.EnterSubGraph`, add a scoped branch: keep `_context` (the parent), call
`_context.BeginLocalContext(targetGraph)` (seeds the local bucket from the target graph's parameters), set
the new pushed frame's `OpenedLocalContext = true`, and keep `FrameContext = _context`. In
`HandleEndNode`, when popping a frame whose `OpenedLocalContext` is `true`, call `_context.EndLocalContext()`
before resuming the parent. Track the flag on `GraphExecutionState` (append-only `bool OpenedLocalContext`,
copied by `ShallowClone`).

**Rationale**: Scopes ride the existing graph stack one-to-one (FR-002), so push/pop of the overlay is
bound exactly to entering/leaving the scope-opening sub-graph — no separate bookkeeping. Sequential
sub-graphs each get a fresh overlay (US1 scenario 3) because each entry calls `BeginLocalContext` anew.
Entering a scope while one is already active (unsupported nested case, FR-011) discards-and-replaces the
existing overlay with a `[GraphCore]` warning — a defined, non-crashing v1 behaviour.

**Alternatives considered**: Public push/pop *events* on the runner for an external scope manager —
rejected: graphcore owns the runner here, so it drives the overlay directly; events would be speculative
API (YAGNI).

## R6 — History / step-back captures the overlay

**Decision**: Extend `BaseContext.DeepClone()` to also copy the local overlay (the `_local` bucket and the
active flag); extend the internal `CopyValuesFrom` to restore both buckets and the active flag. The graph
stack snapshot already deep-copies frames including the new `OpenedLocalContext` flag, so a restored point
reproduces which frames had a scope open. `HistoryEntry.ContextSnapshot` thus carries the full overlay
state.

**Rationale**: FR-010/SC-005 require step-back/checkpoint fidelity across a scope boundary — a discarded
local value must not reappear and an opened local must not linger. Because the live context object is
shared across frames, restore must overwrite its overlay state in place (consistent with the existing
`RestoreContextValues` "copy into the live object, preserve subscribers" approach). Subclasses inherit the
correct behaviour because the overlay is base-managed and copied inside `base.DeepClone()`.

**Alternatives considered**: Snapshotting only resolved values (flattening global+local) — rejected: it
loses which values were local vs. global, so restoring before a scope opened could wrongly keep local
values (fidelity failure).

## R7 — Semver & back-compat strategy

**Decision**: Ship as graphcore **0.3.0 (MINOR)**. The non-breakage proof is operational: the **entire
pre-existing graphcore EditMode suite passes unmodified** after the change (SC-004). No existing test is
edited to accommodate the feature.

**Rationale**: Principle I — new public API and new optional fields are MINOR; nothing is removed or
re-signed. The "suite green, unmodified" rule is the concrete, enforceable expression of "behaviour
identical when no scope is opened."

**Alternatives considered**: PATCH — rejected (new public methods/fields = MINOR by definition). MAJOR —
unnecessary (no breaking change).

## R8 — Headless test execution

**Decision**: EditMode tests only (Principle IV — `BaseRunner`/`BaseContext` are headless). Execute via
Unity 6000.3 batchmode (or Coplay `run_tests`) with the editor closed; delete a stale `Temp/UnityLockfile`
first if present (per the [[graphgameflow]] verification notes).

**Rationale**: All new behaviour (overlay routing, runner lockstep, isolation, durable global write,
step-back) is observable without PlayMode. Matches the established ecosystem verification workflow.
