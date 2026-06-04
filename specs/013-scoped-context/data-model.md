# Phase 1 Data Model: Global & Local Execution Contexts

All changes are **append-only** edits to existing graphcore Runtime types. No new types, no new files in
Runtime (only the four EditMode test files). Naming stays domain-neutral (Principle II).

## Changed type: `BaseContext` (Runtime/Graph/BaseContext.cs)

### New internal state

| Field | Type | Meaning |
|-------|------|---------|
| `_local` | `Dictionary<string, object>` (nullable) | The local overlay bucket; `null` when no scope is open. |
| `_localActive` | `bool` | `true` while a local context is open. (Equivalent to `_local != null`; kept explicit for clarity.) |

The existing `_params` dictionary **is** the global bucket — unchanged. The existing `_subs` subscriber map
is unchanged and shared across both buckets.

### New public methods

| Member | Signature | Behaviour |
|--------|-----------|-----------|
| `BeginLocalContext` | `void BeginLocalContext()` | Opens a fresh, empty local overlay. If one is already open, discards it and opens a new one, logging a `[GraphCore]` warning (unsupported nested-scope case, FR-011). |
| `BeginLocalContext` | `void BeginLocalContext(BaseGraph seedFrom)` | As above, then seeds the new local bucket from `seedFrom`'s declared parameters (same parsing as `InitFromGraph`, but written into the local bucket). |
| `EndLocalContext` | `void EndLocalContext()` | Discards the local overlay (and all its values). No-op + `[GraphCore]` warning if none is open. |
| `HasLocalContext` | `bool HasLocalContext { get; }` | `true` while a local overlay is open. Introspection for runner/tests. |

### Modified method behaviour (signatures unchanged, **not** made virtual)

| Member | Change |
|--------|--------|
| `Set<T>(key, value)` | **Resolve-and-write routing**: if `_localActive` and `_local` contains `key` → write `_local`; else if `_params` contains `key` → write `_params`; else if `_localActive` → write `_local`; else → write `_params`. Fires subscribers exactly as today. Type validation unchanged. |
| `Get<T>(key)` | Read `_local[key]` when active and present; else `_params[key]`; else throw `KeyNotFoundException` as today. |
| `TryGet<T>(key, out v)` | Local-first then global fall-through; returns `false`/`default` only when absent from both. |
| `Has(key)` | `true` if present in the active local **or** in global. |
| `GetAllParameters()` | **Unchanged** — returns the **global** bucket (`_params`) only. Local-context scratch is deliberately excluded so persistence never serializes transient values (a save mid-scope captures global state only). This keeps the existing serialization contract byte-for-byte. |
| `DeepClone()` | Also deep-copies `_local` (when active) and `_localActive` into the clone, so history snapshots capture overlay state. Subclasses inherit this via `base.DeepClone()`. |
| `CopyValuesFrom(source)` (internal) | Also restores `_local`/`_localActive` from `source`, so step-back restores overlay state in place while preserving live subscribers. |

**Invariant (back-compat)**: when `BeginLocalContext` is never called, `_localActive` is `false`, every
branch above collapses to the original `_params`-only behaviour ⇒ identical to graphcore 0.2.0.

**Principle VI note**: subclasses still override only `CreateCloneInstance()` (to return the right subtype);
the overlay is copied by the base `DeepClone`, so subclasses need no extra overlay handling.

## Changed type: `SubGraphNodeData` (Runtime/Nodes/SubGraphNodeData.cs)

| New field | Type | Default | Meaning |
|-----------|------|---------|---------|
| `OpensScope` (`_opensScope`) | `bool` (`[SerializeField] private`) | `false` | When `true`, entering this sub-graph opens a local context (third behaviour). Append-only; pre-existing serialized nodes deserialize to `false` ⇒ unchanged behaviour. |

Existing `TargetGraph` and `InheritParentContext` are **untouched**. Precedence: `OpensScope=true` runs on
the parent context with a local overlay (it implies riding the parent context, so `InheritParentContext` is
not consulted in that branch).

## Changed type: `GraphExecutionState` (Runtime/Execution/GraphExecutionState.cs)

| New field | Type | Default | Meaning |
|-----------|------|---------|---------|
| `OpenedLocalContext` | `bool` | `false` | `true` for a frame whose sub-graph opened a local context, so the runner knows to `EndLocalContext` when this frame is popped. Copied by `ShallowClone` (so history snapshots preserve it). |

## Changed type: `BaseRunner` (Runtime/Execution/BaseRunner.cs)

| Method | Change |
|--------|--------|
| `EnterSubGraph(subNode)` | New branch: if `subNode.OpensScope` → keep `_context` (parent), call `_context.BeginLocalContext(targetGraph)`, push the sub-frame with `FrameContext = _context` and `OpenedLocalContext = true`. The existing `InheritParentContext` (inherit) and fresh-context branches are unchanged and used only when `OpensScope` is false. |
| `HandleEndNode(endNode)` | When popping a sub-graph frame (`_graphStack.Count > 1`): if the popped frame had `OpenedLocalContext == true`, call `_context.EndLocalContext()` before resuming the parent. |

## Lifecycle (happy path)

```
root run            : _context = global only (_localActive = false)         // existing behaviour
enter scoped subA   : BeginLocalContext(subA)  → _local seeded from subA params; OpenedLocalContext = true
  read global key   : falls through _local → _params                        // FR-003, US2.1
  write global key  : resolves in _params → writes global (persists)        // FR-006, US2.2
  write scratch key : not in _local/_params → _local (discarded later)      // FR-004, US1
subA EndNode        : pop frame (OpenedLocalContext) → EndLocalContext()     // _local discarded; FR-005
back in root        : global retains durable writes; scratch gone           // US1.2, US2.3
enter scoped subB   : BeginLocalContext(subB)  → fresh empty _local          // US1.3 sequential reuse
```

## Validation / invariants (testable)

- **I1 Isolation**: after `EndLocalContext`, no key that existed only in `_local` is readable (US1).
- **I2 Fall-through**: while active, a key absent from `_local` resolves from `_params` (US2.1).
- **I3 Durable global**: a write to a key present in `_params` while active persists after end (US2.2/FR-006).
- **I4 Undeclared→local**: a write to a brand-new key while active is discarded on end (US1/FR-004).
- **I5 Back-compat**: with `OpensScope=false` everywhere, no overlay opens; the full pre-existing suite is green (US3/SC-004).
- **I6 Lockstep**: exactly one `BeginLocalContext` per scope-opening frame pushed; exactly one `EndLocalContext` when it is popped (FR-002).
- **I7 Step-back fidelity**: restoring a snapshot taken before a scope opened shows no local values; restoring one taken during a scope reproduces the overlay (FR-010/SC-005).
- **I8 Notifications**: a write to either bucket fires subscribers as before (FR-009).
- **I9 Persistence excludes local**: `GetAllParameters()` returns global only; a snapshot taken while a scope is active contains no local scratch (save never serializes transient values).
