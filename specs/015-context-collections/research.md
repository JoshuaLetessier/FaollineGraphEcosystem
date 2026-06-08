# Phase 0 — Research: P2 Context collections

All decisions respect the constitution (append-only / semver MINOR / universal abstractions / TDD /
simplicity). No NEEDS CLARIFICATION remained from the spec (the three semantic choices are fixed as
Assumptions; implementation forks are resolved here).

## R1 — Storage: separate `_collections` bucket vs. encoding inside `_params`

**Decision**: A new private `Dictionary<string, HashSet<string>> _collections` on `BaseContext`, lazily
allocated, with its **own keyspace** independent from the scalar `_params`.

**Rationale**:
- A set is not a scalar; `_params` stores boxed `bool/int/float/string` and `GetAllParameters` promises a
  scalar-only snapshot. Encoding a set as, say, a serialized string in `_params` would (a) corrupt the
  scalar snapshot/save contract, (b) require ad-hoc (de)serialization on every op, (c) break the
  independent-keyspace requirement (FR-001). 
- A dedicated `HashSet<string>` gives O(1) add/remove/contains and exact set semantics for free.
- Lazy allocation keeps the zero-cost-when-unused guarantee (SC-003).

**Alternatives considered**:
- *Encode sets inside `_params`*: rejected — pollutes the scalar contract and forces serialization.
- *A new public `Collection` type*: rejected (YAGNI) — `HashSet<string>` behind methods is enough; exposing
  a mutable collection type would let callers bypass change notifications.

## R2 — Public API surface, naming, and the change-notification handler

**Decision**: Neutral, `Collection`-prefixed methods on `BaseContext`:
`AddToCollection(key,item)`, `RemoveFromCollection(key,item)`, `CollectionContains(key,item)`,
`CollectionCount(key)`, `GetCollection(key)` (read-only snapshot), `ClearCollection(key)`,
`OnCollectionChanged(key, Action<string>)`, `OffCollectionChanged(...)`. Notifications fire **only on a
real membership change** and iterate a **snapshot** of subscribers (mirroring the scalar `FireSubscribers`).

**Rationale**:
- Neutral naming keeps Principle II (no domain vocabulary in core).
- The handler is `Action<string>` carrying the **collection key** (the changed collection), consistent
  with the scalar `OnParameterChanged(key, Action<object>)` pattern but without a value payload — a set
  change is "this collection changed", and consumers re-query via `GetCollection`/`CollectionCount`. This
  is the cleanest edge-trigger for a future Reactive evaluator (it re-evaluates set state on the signal).
- Fire-on-real-change-only (idempotent add / no-op remove are silent) gives clean edge semantics (FR-006,
  SC-007).

**Alternatives considered**:
- *`Action<string,string,bool>` (key, item, added)*: rejected for v1 — more surface; reactive consumers
  re-query the set anyway. Can be added later (append-only) if a need appears.
- *Return `bool` from Add/Remove (did it change)*: rejected to mirror the `void Set` shape; membership is
  observable via `CollectionContains`/`CollectionCount`.

## R3 — Save surface: parallel `GetAllCollections()` vs. changing `GetAllParameters`

**Decision**: Add `GetAllCollections()` returning
`IReadOnlyDictionary<string, IReadOnlyCollection<string>>` (a snapshot of copies). `GetAllParameters()`
stays scalar-only and its signature/return shape is unchanged.

**Rationale**:
- `GetAllParameters` is a frozen public contract (Principle I) consumed by existing save code; changing it
  would be a breaking change. A parallel accessor is purely additive.
- Returning copies (not live sets) prevents external mutation of internal state and matches the read-only
  intent of a save snapshot.
- The save layer (downstream, e.g. `savesystem.core` integration) composes scalar + collection snapshots;
  the core only exposes both — no new core dependency, per spec Assumptions.

**Alternatives considered**:
- *Fold collections into `GetAllParameters`*: rejected — breaks the frozen scalar contract.
- *Expose live `HashSet`s*: rejected — lets callers mutate internal state and bypass notifications.

## R4 — History: `DeepClone` / `CopyValuesFrom` deep-copy + Principle VI

**Decision**: Extend `DeepClone()` to deep-copy `_collections` (a **new `HashSet<string>` per key**) into
the clone, and extend the internal `CopyValuesFrom()` to clear and rebuild `_collections` from the source
(independent copies). Subscribers (`_collectionSubs`) are **not** copied (mirrors `_subs`). The local
overlay copy logic in `DeepClone`/`CopyValuesFrom` is untouched.

**Rationale**:
- Collections are durable state, so step-back must restore them — exactly like `_params` (FR-007, SC-004).
- Deep copies (new sets) ensure mutating a clone never affects the source (acceptance US2.3).
- Because the copy lives in `BaseContext.DeepClone`/`CopyValuesFrom`, every typed subclass inherits it for
  free and keeps overriding only `CreateCloneInstance` — Principle VI (the same pattern 013 used for the
  overlay and 014 deliberately avoided for transient signals).

**Alternatives considered**:
- *Shallow-copy the `HashSet` references*: rejected — clone and source would share a set, breaking step-back
  isolation.

## R5 — Local-context overlay (0.3.0): global-only

**Decision**: Collections are **global-only**. All collection reads/writes target `_collections`
regardless of `_localActive`; `BeginLocalContext`/`EndLocalContext` do **not** branch, seed, or discard
collections.

**Rationale**:
- The evidenced use (solved-set, inventory, collected) is durable, cross-scene progression state — it is
  inherently global. Routing it through the scalar local/global overlay would add real complexity (a
  second collections bucket, resolve-and-write rules, discard-on-end) with no present need (YAGNI,
  Principle V).
- Keeping collections out of the overlay also keeps the 013 scoped-context feature untouched (Principle I).

**Alternatives considered**:
- *Overlay collections like scalars*: rejected for v1 — speculative; can be added later (append-only) if a
  scoped/local collection need is discovered against a concrete game.

## R6 — graphTest authoring (membership / count / recipe), to promote to graphstandard

**Decision**: Build three authoring classes in `com.faolline.graphTest`:
`TestCollectionContainsCondition` (membership), `TestCollectionCountCondition` (count vs. a value via the
existing `ComparisonOperator`), and `TestRecipeAction` (if a collection contains all required elements,
remove them and add a reward). Exercised by `CollectionExerciseTests`.

**Rationale**:
- Mirrors the 014 `TestSignalPayloadCondition` pattern: domain-neutral, generic nodes that operate on
  `BaseContext`, living in the sandbox now and earmarked for the `graphstandard` buffer lib later.
- Reusing `ComparisonOperator` for the count threshold avoids duplicating comparison logic and previews
  the P4 threshold-Join shape (count ≥ N).

**Alternatives considered**:
- *Put the conditions/recipe in graphcore*: rejected — graphcore holds no concrete `[CreateAssetMenu]`
  authoring nodes; these belong above core (graphstandard), staged in graphTest for now.
- *Create `graphstandard` now*: rejected — out of scope for P2; defer until the standard-node set is
  larger and the promotion is deliberate.
