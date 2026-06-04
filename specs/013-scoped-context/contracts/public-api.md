# Public API Contract: Global & Local Execution Contexts

Authoritative new/changed **public** surface of `com.faolline.graphcore` 0.3.0. Everything here is
additive (semver MINOR). Existing signatures are unchanged.

## `BaseContext` — new public members

```csharp
namespace Faolline.GraphCore
{
    public class BaseContext
    {
        // ── Local-context overlay (new in 0.3.0) ───────────────────────────────

        /// <summary>True while a local context overlay is open.</summary>
        public bool HasLocalContext { get; }

        /// <summary>
        /// Opens a fresh, empty local context layered over the global context.
        /// While open, reads resolve local-first then fall through to global, and writes
        /// are routed local/global by where the key resolves (see routing table).
        /// If a local context is already open it is discarded and replaced (a [GraphCore]
        /// warning is logged — nested local contexts are not supported).
        /// </summary>
        public void BeginLocalContext();

        /// <summary>
        /// As <see cref="BeginLocalContext()"/>, then seeds the new local context from
        /// <paramref name="seedFrom"/>'s declared parameters (same parsing as
        /// <see cref="InitFromGraph"/>, written into the local overlay).
        /// </summary>
        public void BeginLocalContext(BaseGraph seedFrom);

        /// <summary>
        /// Discards the current local context and every value written into it. Values in the
        /// global context are untouched. No-op (with a [GraphCore] warning) if none is open.
        /// </summary>
        public void EndLocalContext();
    }
}
```

`Set` / `Get` / `TryGet` / `Has` / `DeepClone` keep their existing signatures; their behaviour becomes
overlay-aware **only while a local context is open** (see routing). `GetAllParameters()` is the deliberate
exception: it **stays global-only** (unchanged) so persistence never serializes transient local scratch.

## `SubGraphNodeData` — new public member

```csharp
public class SubGraphNodeData : BaseNodeData
{
    /// <summary>
    /// When true, entering this sub-graph opens a local context (a third behaviour alongside
    /// inherit / fresh-blank). Takes precedence over <see cref="InheritParentContext"/>:
    /// a scoped sub-graph runs on the parent context with a local overlay. Default false.
    /// </summary>
    public bool OpensScope { get; set; }   // serialized; default false
}
```

## `GraphExecutionState` — new public member

```csharp
public class GraphExecutionState
{
    /// <summary>True for a frame whose sub-graph opened a local context (so the runner
    /// discards it when this frame is popped). Default false; copied by ShallowClone.</summary>
    public bool OpenedLocalContext { get; set; }
}
```

## Routing table (the core invariant)

Given a key `k`, with `L` = active local bucket (only when `HasLocalContext`), `G` = global bucket:

| Operation | `HasLocalContext` | Condition | Target / Result |
|-----------|-------------------|-----------|-----------------|
| Read `k`  | true  | `k ∈ L` | value from `L` |
| Read `k`  | true  | `k ∉ L`, `k ∈ G` | value from `G` (fall-through) |
| Read `k`  | true  | `k ∉ L`, `k ∉ G` | not found (as today) |
| Read `k`  | false | `k ∈ G` | value from `G` |
| Read `k`  | false | `k ∉ G` | not found (as today) |
| Write `k` | true  | `k ∈ L` | write `L` (shadow) |
| Write `k` | true  | `k ∉ L`, `k ∈ G` | write `G` (**durable global**, FR-006) |
| Write `k` | true  | `k ∉ L`, `k ∉ G` | write `L` (**undeclared → local**, FR-004) |
| Write `k` | false | any | write `G` (single bucket, as today) |

## Behavioural invariants (contract tests)

1. **Back-compat**: with no `BeginLocalContext` call, every `BaseContext` operation is identical to 0.2.0;
   the full pre-existing graphcore EditMode suite passes unmodified. (SC-004)
2. **Isolation on end**: keys that existed only in the local bucket are gone after `EndLocalContext`. (US1)
3. **Fall-through reads**: a key absent locally resolves from global while a scope is open. (US2.1)
4. **Durable global write**: writing a key that lives in global, from inside a scope, persists past
   `EndLocalContext`. (US2.2 / FR-006)
5. **Undeclared → local**: writing a brand-new key inside a scope is discarded on end. (US2.3 / FR-004)
6. **Runner lockstep**: `OpensScope=true` ⇒ exactly one `BeginLocalContext(targetGraph)` on entry and one
   `EndLocalContext` on that sub-graph's end; `OpensScope=false` ⇒ no overlay (inherit/fresh unchanged).
   (FR-002 / FR-008)
7. **Sequential reuse**: two scope-opening sub-graphs in sequence each get a fresh, empty local. (US1.3)
8. **Step-back fidelity**: history snapshot + restore reproduces overlay state across a scope boundary —
   no discarded local value reappears, no closed local lingers. (FR-010 / SC-005)
9. **Notifications**: writes to either bucket fire `OnParameterChanged` subscribers as before. (FR-009)
10. **Nested-scope guard**: `BeginLocalContext` while one is open discards-and-replaces with a `[GraphCore]`
    warning; it never throws or corrupts state. (FR-011)
11. **Persistence excludes local**: `GetAllParameters()` returns the global bucket only; a snapshot taken
    while a scope is active contains no local scratch values. (save serializes durable state only)
