# Feature Specification: P2 — Context collections (named string-sets)

**Feature Branch**: `015-context-collections`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "P2 — Context collections: add mutable named collections (sets of strings) to graphcore's BaseContext so a graph can hold set-valued state (solvedPuzzles, collectedItems, inventory) beside the 4 scalars. Add/remove/contains/count/enumerate/clear, change notifications, durable (save + history). Prerequisite for the Reactive engine (P3). Exercised in graphTest with a membership condition, a count-threshold condition, and a recipe (consume-set → produce) action. Append-only / semver MINOR 0.4.0→0.5.0 / EditMode TDD / headless."

## User Scenarios & Testing *(mandatory)*

**Actors**
- **Graph author** — designs a graph and reads/writes set-valued state (which ids have been collected, solved, owned).
- **Host integrator** — drives a graph and reads collection state (e.g. to render an inventory) or persists it.
- **(transitive) downstream-lib developer** — builds a domain lib (quest/inventory) on graphcore and inherits the capability.

### User Story 1 - Hold and mutate set-valued state (Priority: P1) 🎯 MVP

A graph can keep a named **set of string ids** in the context and mutate it element-wise: add an element
(idempotent — no duplicates), remove an element (no-op if absent), test membership, count, enumerate the
members, and clear. The collection keyspace is independent from the scalar parameter keyspace.

**Why this priority**: This is the irreducible core — without set state, no real game progression
(solved-set, inventory, collected) can be modelled. Everything else builds on it.

**Independent Test**: Add "a","b","a" to collection "items" → count is 2, contains "a" true, contains "c"
false; remove "a" → count 1; enumerate yields {"b"}; clear → count 0.

**Acceptance Scenarios**:

1. **Given** an empty context, **When** the author adds "a" then "b" then "a" to collection K, **Then** the count of K is 2 and it contains "a" and "b".
2. **Given** K contains "a", **When** the author removes "a", **Then** K no longer contains "a" and its count drops by one.
3. **Given** K does not exist, **When** the author queries contains/count/enumerate on K, **Then** the result is false / 0 / empty and no error occurs.
4. **Given** a scalar parameter and a collection that share the same key string, **When** both are used, **Then** they are independent (mutating one never affects the other).

---

### User Story 2 - Durable across history and save (Priority: P1)

Collection state is **durable** (unlike P1 transient signals): a step-back restores the exact membership
a collection had at the snapshot, and the full set of collections is exposed for saving through a
dedicated accessor — while the existing scalar snapshot (`GetAllParameters`) stays scalar-only and
unchanged.

**Why this priority**: Progression state must survive undo and save. "The collection IS the save" — a
saved solved-set is the serialization of progress; this story makes that real and is required before any
reactive/quest layer.

**Independent Test**: Add ids, snapshot (advance a node), add more, step back → the collection holds only
the pre-snapshot ids. A save snapshot lists all collections; the scalar snapshot lists none of them.

**Acceptance Scenarios**:

1. **Given** collection K with {"a"} captured in history, **When** "b" is added and then execution steps back to that point, **Then** K holds exactly {"a"}.
2. **Given** collections exist, **When** the save snapshot is taken, **Then** it includes every collection with its current members, **and** the scalar-parameter snapshot includes none of them.
3. **Given** a deep copy of a context, **When** the copy's collection is mutated, **Then** the original's collection is unchanged (independent copies).

---

### User Story 3 - React to collection changes (Priority: P2)

A change to a collection (an element actually added or removed) fires a per-key change notification, so
logic can re-evaluate when set state changes. Subscribing/unsubscribing mirrors the existing scalar
change-notification pattern and is re-entrant safe.

**Why this priority**: Enables the future Reactive engine (P3) to recompute on state change. Useful but
US1+US2 already deliver usable, persistable set state; this can land second.

**Independent Test**: Subscribe to K; add a new element → handler fires once; add the same element again →
handler does not fire (no real change); remove it → fires once.

**Acceptance Scenarios**:

1. **Given** a subscriber on collection K, **When** a new element is added, **Then** the subscriber is notified once.
2. **Given** K already contains "a", **When** "a" is added again, **Then** no notification fires (membership unchanged).
3. **Given** a subscriber on K, **When** an element is removed that was present, **Then** the subscriber is notified once; removing an absent element fires nothing.

---

### User Story 4 - Author with collections (Priority: P2)

Authors can gate edges and run effects from collection state, demonstrated in the graphTest sandbox: a
**membership** condition ("K contains X"), a **count-threshold** condition ("count(K) ≥ N"), and a
**recipe** action (when K contains all required elements, consume them and add a reward element) — the
fusion/crafting pattern.

**Why this priority**: Proves collections are usable in real authoring, and validates the membership /
count / consume-produce shapes the Reactive engine and quest libs will need. Builds on US1.

**Independent Test**: Build a graph whose edge is gated by "K contains key"; with K={"key"} the edge is
taken. A recipe over {"x","y"} produces "z": after running it on a context holding {"x","y"}, the context
holds {"z"} and no longer "x"/"y".

**Acceptance Scenarios**:

1. **Given** an edge gated by a membership condition on K, **When** K contains the element, **Then** the edge is traversable; otherwise it is not.
2. **Given** an edge gated by a count-threshold condition (≥ N), **When** count(K) reaches N, **Then** the condition passes.
3. **Given** a recipe requiring {"x","y"} producing "z", **When** it runs on a context holding both, **Then** the context holds "z" and no longer holds "x" or "y"; **When** a required element is missing, **Then** the recipe makes no change.

---

### Edge Cases

- **Add an element already present** → no-op, membership/count unchanged, no notification.
- **Remove an absent element / from an absent collection** → no-op, no notification.
- **Contains / count / enumerate on an absent collection** → false / 0 / empty, never an error.
- **Clear an absent or empty collection** → no-op.
- **Same key used for a scalar and a collection** → independent keyspaces; neither affects the other.
- **Local context open (0.3.0 overlay)** → collection reads/writes still target the single global store; opening/ending a local scope does not branch or discard collections.
- **Re-entrant change** (a notification handler mutates a collection) → handlers iterate a stable snapshot.
- **Null/empty collection key or null element** → `[GraphCore]` warning, safe no-op.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: BaseContext MUST store named collections of strings keyed by a string, in a keyspace independent from scalar parameters.
- **FR-002**: It MUST support adding an element (idempotent — a set, no duplicates), creating the collection on first add.
- **FR-003**: It MUST support removing an element (a no-op when the element or collection is absent).
- **FR-004**: It MUST answer membership (contains), cardinality (count), and read-only enumeration of a collection's current members; absent collection ⇒ false / 0 / empty.
- **FR-005**: It MUST support clearing a collection (emptying its members).
- **FR-006**: An actual membership change (a new element added, or a present element removed) MUST fire a per-key change notification; subscribe/unsubscribe MUST mirror the scalar pattern and be re-entrant safe (snapshot-on-fire). An idempotent add or no-op remove MUST NOT fire.
- **FR-007**: Collections MUST be captured by history deep-clone and restored on step-back and on history restore, as independent copies (mutating a clone never affects the source).
- **FR-008**: A dedicated read-only snapshot accessor MUST expose all collections (key → members) for saving; the scalar snapshot (`GetAllParameters`) MUST remain scalar-only and unchanged.
- **FR-009**: Collections MUST be global state: not subject to the 0.3.0 local-context overlay; reads/writes always target the single global collection store regardless of whether a local context is open.
- **FR-010**: Invalid use (null/empty collection key, null element) MUST be handled with a `[GraphCore]`-prefixed warning and a safe no-op, never an unhandled exception.
- **FR-011**: When a graph uses no collections, runtime/context behaviour MUST be identical to the pre-feature (0.4.0) behaviour (full back-compatibility).
- **FR-012**: All additions MUST be append-only — no existing public signature, serialized field, or behaviour removed or changed (semver MINOR, graphcore 0.4.0 → 0.5.0). The change MUST be inherited automatically by `BaseContext` subclasses (Principle VI) without each subclass altering its clone logic beyond the existing `CreateCloneInstance` override.
- **FR-013**: The capability MUST be verifiable in `com.faolline.graphTest`, exercising a membership condition, a count-threshold condition, and a recipe (consume-set → produce) action.

### Key Entities

- **Collection**: a named set of unique string elements held in the context; identity = its key; value = its current members. Durable (persisted, history-captured).
- **Collection subscriber**: a party notified when a named collection's membership actually changes.
- **Recipe** (graphTest authoring concept): a set of required elements and a reward element; running it consumes the required set (if all present) and adds the reward.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An author can accumulate and query set state end-to-end (add several ids, count, test membership, remove, enumerate, clear) using only public API.
- **SC-002**: 100% of the existing graphcore EditMode suite (560 tests, incl. P1 signals) passes **unmodified** after the change (non-breakage gate).
- **SC-003**: A context that uses no collections exhibits zero observable difference from 0.4.0 behaviour.
- **SC-004**: A step-back across collection mutations restores the exact pre-snapshot membership.
- **SC-005**: A save snapshot includes every collection with its members, while the scalar snapshot includes none of them.
- **SC-006**: graphTest demonstrates, all green headless: a membership-gated edge, a count-threshold-gated edge, and a recipe that consumes a required set and yields a reward.
- **SC-007**: A real membership change fires exactly one notification (and an idempotent add / no-op remove fires none), enabling reactive subscription.

## Assumptions

- **String elements, set semantics** for v1: unique elements, idempotent add, membership/count/enumerate/clear. Lists, multisets, ordering, and non-string element types are out of scope.
- **Global-only**: collections are not routed through the 0.3.0 local-context overlay; they are durable global progression state. Per-scope/local collections are deferred.
- **Durable**: collections live in history snapshots and in the save surface (a parallel accessor), distinct from P1 signals which are transient and excluded from both.
- **Notifications fire on real change only** (idempotent operations are silent), to give reactive consumers clean edge-triggered semantics.
- **Save composition is downstream**: the core exposes both the scalar snapshot and the collection snapshot; combining them into a saved blob is the save layer's job (no new core dependency).
- **graphTest hosts the authoring nodes** (membership/count conditions, recipe action) for now; they are candidates to promote to the future `graphstandard` buffer lib.
- **Governance per constitution**: EditMode tests failing-first (TDD); `[GraphCore]` prefix; one class per file; XML docs on new public API; additions append-only and semver-MINOR.

## Out of Scope *(deferred)*

- List/multiset/ordered collections; non-string element types (int/float/bool sets).
- Collections participating in the local-context overlay (scoped local collections).
- The dedicated `graphstandard` buffer lib (the membership/count conditions and recipe action are exercised in graphTest for now, to be promoted later).
- The generic threshold-Join **node** (roadmap P4), the Reactive engine (P3), and the Time node (P5).
