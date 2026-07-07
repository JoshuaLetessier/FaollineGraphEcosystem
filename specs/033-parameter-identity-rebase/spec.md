# Feature Specification: Parameter identity re-base — typed ParameterName asset + generated constants

**Feature Branch**: `033-parameter-identity-rebase`

**Created**: 2026-07-07

**Status**: Implemented 2026-07-08 (graphcore 0.34.0). Direction (B); all decisions (D1–D6) resolved 2026-07-07.

**Input**: Design discussion (2026-07-07), the parameter follow-up to the signal re-base (spec `032`). Parameters
are the last of the three context primitives still on raw-string identity, and the only one that requires a
per-graph declaration. This feature applies the proven `032` model (stable GUID + cosmetic display name +
generated constants + islands) to parameters, and — because a parameter carries a **type** and a **default**
that a signal does not — folds the parameter's declaration into the asset so the three primitives finally share
one identity model. The capability gap ("a parameter cannot wake the runner") is **explicitly out of scope**
(a separate graphstandard / execution-paradigms track — condition-awaits, not covered here).

---

## Problem statement

Three buckets, from the discussion:

1. **Identity / ergonomics (a solved template).** `SetIntAction`, `AddIntAction`, `IntCompareCondition`, … all
   reference a **raw string** `_parameterKey` ("must match a key declared on the graph's Parameters list"). No
   autocompletion, rename-unsafe, and a typo reads the type default silently (0 / false / ""). This is exactly
   the fragility `032` closed for signals; the remedy (GUID asset + display name + generated constants +
   islands) is now a de-risked template.

2. **The declaration asymmetry (the genuinely new problem).** A parameter is **declared** on the graph's
   `_parameters` list with its **type** and **default**, and `InitFromGraph` seeds the context. It is the ONLY
   primitive requiring declaration (signals/collections spring into existence on first use), so it lives in two
   places linked by string-matching: the declaration (graph) and the reference (action). Fixing identity alone
   is not enough — we must decide **where the type and default live**.

3. **Capability (out of scope).** "A parameter cannot wake the runner" (only signals/time can). This is not an
   identity problem and this feature does not touch it. It belongs to the condition-await direction in
   graphstandard. Recorded here only to fence it off.

Decisive framing (from the discussion): the three primitives were split by **storage shape** (value cell /
event / set), but the axis that matters is **capability** (durable value? wake? latch?). This feature unifies
their **identity**; it deliberately does NOT merge parameter (a quiet durable typed value) and signal (a
wake-event + latch), because forcing that merge re-creates the exact quiet-write vs wake-write distinction that
separates them. "Typed signals" is true of the *skin* (identity), not the *semantics*.

---

## Endpoint (direction B, validated)

A **`ParameterName` asset is the typed parameter definition**: a stable GUID (`Key`, `OnEnable`, never
editable, `IStableGuidIdentity` — so the duplicate detector and `StableGuidPersistence` cover it), a cosmetic
`DisplayName`, a `ParameterType`, and a default value of that type. Actions/conditions reference the asset
(implicit `(string)` → the GUID, the runtime key). The asset's **type is authoritative** — enabling type-safety
at authoring that raw strings never allowed (today nothing stops `SetInt("hp")` and `SetString("hp")` colliding
on one key). Pure host code raises/reads through generated **`GraphParams`** constants (symbol from
`DisplayName`, value = GUID). Raw-string parameters remain an ungoverned **islands** escape hatch. Renaming a
parameter's display name is free: the GUID (sets/gets/saves) never changes; only the regenerated code symbol
does (stale code breaks at compile).

Unpublished ecosystem — **clean break, no migration** (regenerate consumer content), as with `032`.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Renaming a parameter never breaks the data (Priority: P1)

Actions and conditions reference the same `ParameterName` asset; they read/write it on the shared context,
matched on the asset's stable GUID. Renaming the parameter's display name (or the asset file) leaves every set,
get, comparison, and saved value matching — only the regenerated `GraphParams` symbol changes.

**Independent Test**: a `SetIntAction` and an `IntCondition` referencing the same asset round-trip a value;
renaming `DisplayName` keeps them matching and `Key` unchanged.

### User Story 2 - Type-safety at authoring (Priority: P1)

Because the asset carries the authoritative type, wiring a `SetIntAction` to a `Float` parameter (or two
actions of different types onto the same parameter) is caught — by a validator and/or by the inspector only
offering type-compatible parameters — instead of silently corrupting a key at runtime.

**Independent Test**: a validator flags (or the typed field prevents) a type-mismatched parameter reference.

### User Story 3 - Pure code references a compile-checked parameter (Priority: P1)

Host code reads/writes a parameter through `GraphParams.Hp` (a generated constant whose value is the GUID)
instead of a raw literal — compile-checked, autocompleted, rename-safe via regeneration.

**Independent Test**: generating constants yields `GraphParams.Hp` = the asset's GUID; `context.Set<int>(GraphParams.Hp, …)` writes the same key an asset-based action reads.

### User Story 4 - Raw-string parameters stay viable as an escape hatch (Priority: P2)

`context.Set<int>("hp", …)` with a raw literal keeps working (the param store is string-keyed) for dynamic /
quick use, explicitly ungoverned; per islands it does not interoperate with GUID-keyed asset parameters.

---

## Requirements *(mandatory)*

- **FR-001**: `ParameterName : ScriptableObject, IStableGuidIdentity` — stable GUID `Key` (OnEnable, never
  editable, persisted via `StableGuidPersistence`), cosmetic `DisplayName`, a `ParameterType`, and a typed
  default; `(string)ParameterName` returns the GUID.
- **FR-002**: The stock actions/conditions (`SetIntAction`, `AddIntAction`, `SetBool/Float/StringAction`,
  `ToggleBoolAction`, `SetRandomIntAction`, `Int/Float/Bool/StringCondition`, the `*CompareCondition`s)
  reference `ParameterName` asset(s) instead of raw `_parameterKey` strings; raw-string overloads on
  `BaseContext` stay as the islands escape hatch.
- **FR-003**: A **`GraphParams` generator** (mirror of `SignalConstantsGenerator`) emits a `const string` per
  `ParameterName` asset — symbol from `DisplayName`, value = GUID — with blocking collision errors. (Likely
  shares the generator infrastructure with signals; see D3.)
- **FR-004**: Authoring type-safety — a type-mismatched parameter reference is caught (validator and/or typed
  inspector field; see D5).
- **FR-005**: `ParameterName` participates in the stable-id duplicate detector (via `IStableGuidIdentity`).

**Out of scope**: the capability gap (parameters waking the runner) — a separate condition-await feature in
graphstandard. Collections are untouched.

---

## Key design decisions — to resolve before implementation

- **D1 — Defaults & seeding (STRUCTURING; resolve first).** A signal has no default; a parameter does (hp
  starts at 100). Today `BaseContext.InitFromGraph(graph)` eagerly seeds `_params` from the graph's
  `_parameters` list (4 real callers: `BaseRunner` sub-graph, `DialoguePlayer`, `GraphFlowDriver`, the test
  editor). Under a declaration-free (B), where does the initial value come from?
  - *(a) Lazy from asset via a runtime registry* — `Get`/`TryGet` on an unset GUID looks up the `ParameterName`
    asset's default. **Rejected**: this is the runtime-name-resolution trap `032` condemned (loads all assets,
    runtime cost).
  - *(b) Referenced-asset scan at `InitFromGraph`* — walk the graph's actions/conditions, collect referenced
    `ParameterName`s, seed their defaults. Declaration-free AND auto-seeded, but reflection-heavy and misses a
    parameter used only from host code.
  - *(c) Explicit-only (no auto-seed)* — parameters start unset (like signals/collections start empty); the
    asset's default is an **editor/codegen hint**, and the consumer sets initial values via a start-node action
    or host code (optionally a convenience "seed defaults" action referencing the assets to seed). Most
    consistent with the declaration-free philosophy; costs the current auto-init ergonomics.
  - *(d) Keep a per-graph declaration list keyed by `ParameterName`* (with an optional per-graph default
    override) that `InitFromGraph` seeds. Pragmatic, keeps auto-seed and per-graph defaults, low-risk — but the
    list persists, so this is effectively **(C) not (B)**: parameters stay the declared odd-one-out.
  **→ RESOLVED (2026-07-07): (b) referenced-scan via an interface.** Param-referencing actions/conditions
  implement a small opt-in contract (`IParameterReferencing` — `IEnumerable<ParameterName> ReferencedParameters`);
  `InitFromGraph` walks the graph's action/condition sites (node enter/exit actions, entry/resume conditions,
  edge and choice conditions), collects the referenced `ParameterName`s, and seeds each one's default (from the
  asset) **only if not already set**. Declaration-free AND auto-seeded — the graph keeps no `_parameters` list.
  The default is **global** (on the asset), not per-graph; a per-graph exception is an enter-action `SetX`. The
  ~24 stock actions/conditions implement the interface once; a custom action that doesn't opt in simply leaves
  its params unset (the host sets them), which is safe. Rejected: (a) runtime registry (resolution trap),
  (c) explicit-only (loses auto-seed ergonomics), (d)/(C) declaration list (params stay the only declared
  primitive — no full unification).

- **D2 — Type-mismatch handling. → RESOLVED: validator (+ inspector hint).** Since D5 chose a single asset with
  a `ParameterType` field, type-safety is enforced at validate time — a `SetIntAction` referencing a `Float`
  `ParameterName` (or two differently-typed actions on the same asset) is a validator finding. The inspector may
  additionally hint/filter, but the validator is the guarantee.

- **D3 — Codegen sharing. → RESOLVED: share the core.** `GraphParams` reuses `SignalConstantsGenerator`'s
  `Sanitize` + collision logic (extract the shared core, or a common base); it emits a separate `GraphParams`
  class (symbol from `DisplayName`, value = GUID). One menu `Faolline ▸ Parameters ▸ Generate Constants`.

- **D4 — Islands. → RESOLVED: yes (by analogy with 032).** Asset parameters key on the GUID; raw-string
  `Set/Get(literal)` is a separate literal channel that does not cross.

- **D5 — One `ParameterName` (with a `ParameterType` field) vs typed subclasses. → RESOLVED: single asset +
  validator.** One `ParameterName` carrying a `ParameterType` enum + typed default, with type-safety enforced by
  the validator (D2) — simplest, mirrors `SignalName`. Typed subclasses (compile-time field safety) are a
  possible later hardening, not now.

- **D6 — Migration. → RESOLVED: clean break (unpublished).** Drop the graph `_parameters` list and
  `ParameterData`'s role as a graph declaration (the type+default move onto `ParameterName`); migrate the ~24
  actions/conditions to reference `ParameterName`; regenerate consumer content. No compat shim.
  **Impact analysis (2026-07-07, confirmed by the user):** the 108 raw-key call sites break down as ~92 in
  test code + ~16 in sample builders — the core execution logic only *defines* the properties, it never calls
  them, so the runtime is small/clean to change. Two additional core-editor consequences of dropping the graph
  declaration: the graph-parameter authoring panel (`BaseNodeInspectorView.AddParameter/…`) and
  `ParameterDataDrawer` become obsolete (parameters are now `ParameterName` project assets dragged onto actions,
  exactly like `SignalName`) and are removed. The **raw-string escape hatch stays at the `BaseContext` API
  level** (`context.Set<int>("hp", …)`) and in the graphTest `Test*` doubles — so those doubles and their tests
  need no change; only the *governed* graphcore actions/conditions lose their raw string field.

---

## Rationale trail

- **Why not merge parameter and signal into one "typed signal"?** They share identity under this feature but
  not semantics: a parameter is a quiet durable typed value, a signal a wake-event + durable latch. A full
  merge needs to distinguish a silent set from a waking set — which is exactly the parameter/signal split
  reappearing. The split is load-bearing.
- **Why the capability gap is out of scope**: making a parameter wake the runner is a *capability* addition
  (condition-awaits), orthogonal to identity, and belongs to graphstandard's non-linear engines, not this
  re-base. Mixing them would couple two refactors.
- **Why the seeding decision is central**: it is the one place parameters genuinely differ from signals
  (a default + eager seeding with 4 live callers), and it determines whether "declaration-free" is actually
  reachable or whether (C) is the honest landing.
