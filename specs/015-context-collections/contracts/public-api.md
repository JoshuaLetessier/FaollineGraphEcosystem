# Phase 1 — Public API Contract: P2 Context collections

Authoritative new public surface for graphcore 0.5.0 and the testable invariants each member honors.
Everything is **additive**; no existing member changes. All on `Faolline.GraphCore.BaseContext`.

## New methods

```csharp
public void AddToCollection(string key, string item);
public void RemoveFromCollection(string key, string item);
public bool CollectionContains(string key, string item);
public int  CollectionCount(string key);
public IReadOnlyCollection<string> GetCollection(string key);
public void ClearCollection(string key);
public void OnCollectionChanged(string key, Action<string> handler);
public void OffCollectionChanged(string key, Action<string> handler);
public IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetAllCollections();
```

## Invariants

- INV-1 (set semantics): `AddToCollection` is idempotent; `CollectionCount` equals the number of distinct
  items added and not removed; `CollectionContains` reflects membership.
- INV-2 (absent-safe): `CollectionContains`→`false`, `CollectionCount`→`0`, `GetCollection`→empty
  (non-null), `RemoveFromCollection`/`ClearCollection`→no-op for an absent collection; never throws.
- INV-3 (independent keyspace): a scalar parameter and a collection sharing a key string never interfere.
- INV-4 (change-only notify): `OnCollectionChanged` handlers fire exactly once per **real** membership
  change (new add / present-remove / non-empty clear), with the collection key; idempotent add and no-op
  remove fire nothing. Delivery iterates a subscriber snapshot (re-entrant safe).
- INV-5 (read-only exposure): `GetCollection` and `GetAllCollections` return copies/read-only views;
  mutating them does not affect context state.
- INV-6 (overlay-independent): every collection op targets the global store regardless of
  `HasLocalContext`; `BeginLocalContext`/`EndLocalContext` never branch/seed/discard collections.
- INV-7 (save separation): `GetAllCollections` lists all collections; `GetAllParameters` is unchanged and
  excludes them.
- INV-8 (durable clone): `DeepClone` produces independent collection copies (clone↔source share no set);
  `CopyValuesFrom` restores collections from a snapshot; subscribers are not cloned.
- INV-9 (guards): null/empty key or null item → `[GraphCore]` warning + safe no-op / absent-result.
- INV-10 (back-compat): a context using no collections behaves byte-for-byte as 0.4.0; existing public
  members (`Set/Get/GetAllParameters`, overlay, signals) are unchanged.

## graphTest authoring contract (verification, not core)

- `TestCollectionContainsCondition` — passes iff `CollectionContains(key, item)` (optional negate).
- `TestCollectionCountCondition` — passes iff `CollectionCount(key)` compares to a value via
  `ComparisonOperator`.
- `TestRecipeAction` — if `CollectionContains(key, r)` for every required `r`, then remove each required
  and `AddToCollection(key, reward)`; otherwise no change.

## Acceptance → invariant traceability

| Spec acceptance | Invariant(s) |
|-----------------|--------------|
| US1.1 add idempotent + count | INV-1 |
| US1.2 remove drops membership | INV-1 |
| US1.3 absent queries safe | INV-2 |
| US1.4 keyspace independence | INV-3 |
| US2.1 step-back restores membership | INV-8 |
| US2.2 save includes collections, scalars don't | INV-7 |
| US2.3 deep copy independence | INV-8 |
| US3.1 notify on add | INV-4 |
| US3.2 silent on idempotent add | INV-4 |
| US3.3 notify on present-remove, silent on absent | INV-4 |
| US4.1 membership-gated edge | INV-2 (+ graphTest) |
| US4.2 count-threshold edge | INV-1 (+ graphTest) |
| US4.3 recipe consume→produce | INV-1/INV-2 (+ graphTest) |
| Edge: local context open | INV-6 |
| SC-002 suite green unmodified | INV-10, INV-7 |
