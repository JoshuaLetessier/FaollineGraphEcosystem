# Phase 1 — Data Model: P2 Context collections

Exact type changes, invariants, and semantics. All changes are **append-only** (semver MINOR, graphcore
0.4.0 → 0.5.0). Existing public signatures are unchanged. All changes are on `BaseContext`
(`Runtime/Graph/BaseContext.cs`); no new public type.

## `BaseContext` — collection store (additions only)

New private fields (lazily allocated):
- `Dictionary<string, HashSet<string>> _collections` — the named string-sets (own keyspace).
- `Dictionary<string, List<Action<string>>> _collectionSubs` — per-collection change subscribers.

New public methods:
- `void AddToCollection(string key, string item)` — adds `item` to collection `key` (created on first add);
  idempotent (no duplicates).
- `void RemoveFromCollection(string key, string item)` — removes `item`; no-op if item/collection absent.
- `bool CollectionContains(string key, string item)` — membership; `false` if collection absent.
- `int CollectionCount(string key)` — cardinality; `0` if collection absent.
- `IReadOnlyCollection<string> GetCollection(string key)` — read-only **snapshot** (copy) of members;
  empty when absent (never null).
- `void ClearCollection(string key)` — empties the collection.
- `void OnCollectionChanged(string key, Action<string> handler)` — subscribe to membership changes.
- `void OffCollectionChanged(string key, Action<string> handler)` — unsubscribe.
- `IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetAllCollections()` — read-only snapshot of
  all collections (copies) for saving.

**Invariants**
- C1 — **Independent keyspace**: `_collections` is separate from `_params`. A scalar parameter and a
  collection may share the same key string with no interference (FR-001, acceptance US1.4).
- C2 — **Set semantics**: `AddToCollection` is idempotent (a `HashSet`, no duplicates);
  `RemoveFromCollection` of an absent item is a no-op; `CollectionContains`/`CollectionCount`/
  `GetCollection` on an absent collection return `false`/`0`/empty (FR-002…FR-005).
- C3 — **Change-only notification**: a notification fires **iff** membership actually changed — a new item
  added, a present item removed, or a non-empty collection cleared. Idempotent add and no-op remove fire
  nothing. The handler receives the **collection key** (FR-006, SC-007).
- C4 — **Re-entrant safe**: subscribers are invoked over a snapshot copy of the list (mirrors
  `FireSubscribers`); subscribe/unsubscribe during delivery does not corrupt iteration.
- C5 — **Read-only exposure**: `GetCollection` and `GetAllCollections` return copies/read-only views;
  callers cannot mutate internal state through them.
- C6 — **Global-only / overlay-independent**: all collection operations target `_collections` regardless
  of `_localActive`. `BeginLocalContext`/`EndLocalContext` do not branch, seed, or discard collections
  (FR-009).
- C7 — **Save separation**: `GetAllCollections()` exposes collections; `GetAllParameters()` is unchanged
  and scalar-only (FR-008).
- C8 — **Null/empty guards**: a null/empty `key` or null `item` on any mutating/query method logs a
  `[GraphCore]` warning and is a safe no-op (queries return the absent-collection result) (FR-010).
- C9 — **Durable clone**: `DeepClone()` deep-copies `_collections` (a new `HashSet` per key); subscribers
  are not copied. `CopyValuesFrom()` clears and rebuilds `_collections` from the source as independent
  copies. Clone/source never share a set (FR-007, acceptance US2.3).
- C10 — **Subclass inheritance (Principle VI)**: the deep-copy lives in `BaseContext`, so subclasses
  inherit it via `base.DeepClone()` and keep overriding only `CreateCloneInstance`.

## Method behaviour detail

| Method | Empty/absent key behaviour | Fires notification? |
|--------|----------------------------|---------------------|
| `AddToCollection` | null/empty key or null item → `[GraphCore]` warn + no-op | iff item was newly added |
| `RemoveFromCollection` | absent item/collection → no-op | iff item was present and removed |
| `ClearCollection` | absent/empty → no-op | iff it had ≥1 member |
| `CollectionContains` | absent → `false` | n/a |
| `CollectionCount` | absent → `0` | n/a |
| `GetCollection` | absent → empty `IReadOnlyCollection<string>` | n/a |
| `GetAllCollections` | none → empty dictionary | n/a |

## Unchanged (explicitly)

- `_params`, `Set/Get/TryGet/Has`, `GetAllParameters`, `OnParameterChanged`/`OffParameterChanged`,
  `InitFromGraph`, the local-context overlay (`_local`/`_localActive`, `BeginLocalContext`/
  `EndLocalContext`/`HasLocalContext`), and the 014 signal channel — all unchanged.
- `DeepClone`/`CopyValuesFrom` keep their existing `_params` + overlay logic; the collection copy is added
  alongside it.
