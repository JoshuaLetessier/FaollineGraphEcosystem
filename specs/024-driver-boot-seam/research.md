# Phase 0 — Research: driver boot configuration seam

## R1 — An overload, not settable properties or an event

**Decision**: Add `public void Boot(GameFlowContext context, NodeExecutorRegistry registry)`. Keep `Boot()`
(delegates to `Boot(null, null)`).

**Rationale**: The need is "prepare a context + registry, then boot on them." An explicit overload expresses
exactly that, is append-only (the no-arg `Boot()` is untouched, different arity → no ambiguity), and avoids
ordering pitfalls of settable `Context`/`Registry` properties (which would have to be read at boot anyway) or
an `OnConfiguring` event (more surface for the same outcome).

**Alternatives**: *settable `Context`/`Registry` before boot* — rejected: implicit ordering, two ways to do
one thing. *`OnConfiguring`/`OnBeforeStart` event* — deferred: the overload covers the need (YAGNI).

## R2 — Provided-context contract: use as-is, fill SceneLoader if absent, no InitFromGraph

**Decision**: When `context != null`, the driver assigns it as the live context, sets
`context.SceneLoader = SceneLoader` only when the context's loader is null, and does **not** call
`InitFromGraph`. When `context == null`, it creates a fresh `GameFlowContext`, sets its `SceneLoader`, and
calls `InitFromGraph(graph)` — the current behavior.

**Rationale**: Providing a context means "I seeded it" — running `InitFromGraph` would overwrite seeded
declared parameters with the graph's defaults, defeating the purpose. Filling the scene loader only when
absent keeps `LoadSceneAction` working without clobbering a caller-set loader. The no-context path is byte-for-
byte the prior behavior.

**Alternatives**: *always `InitFromGraph`* — rejected: clobbers the caller's seeding. *never set the loader* —
rejected: a freshly-built context would then fail scene loads.

## R3 — Registry contract

**Decision**: `BaseRunner.Start(graph, context, registry ?? new NodeExecutorRegistry())`. A provided registry
makes custom executors active; a null registry yields the current empty-registry behavior.

**Rationale**: Minimal and symmetric with the context handling; preserves the no-arg behavior.

## R4 — `Boot()` preserved by delegation; guards shared

**Decision**: Extract the boot body into `private void BootInternal(GameFlowContext, NodeExecutorRegistry)`
holding the guards (no graph / no valid start / already running → the same `[GraphGameFlow]` warnings) and the
context/registry logic. `Boot()` → `BootInternal(null, null)`; `Boot(ctx, reg)` → `BootInternal(ctx, reg)`.

**Rationale**: One code path for both entry points guarantees identical guards and lifecycle; `Boot()` keeps
its exact signature and behavior (append-only).
