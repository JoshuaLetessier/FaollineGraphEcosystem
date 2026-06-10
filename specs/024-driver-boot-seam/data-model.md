# Phase 1 — Data Model: driver boot configuration seam

Additive to `GraphFlowDriver` (`Faolline.GraphGameFlow`). No new types; graphcore/graphstandard untouched.

## GraphFlowDriver — added/changed

| Member | Kind | Description |
|--------|------|-------------|
| `Boot(GameFlowContext, NodeExecutorRegistry)` | NEW public overload | Boots on the provided context + registry (nulls fall back to the fresh-context / empty-registry behavior). |
| `Boot()` | unchanged signature | Now delegates to `BootInternal(null, null)` — identical behavior. |
| `BootInternal(GameFlowContext, NodeExecutorRegistry)` | NEW private | The shared boot path: guards + context/registry resolution + subscribe + `runner.Start`. |

## Boot resolution

```
BootInternal(context, registry):
  guard: already running / no graph / no valid start  → [GraphGameFlow] warning, stay inert   (unchanged)
  if context != null:
      _context = context
      if _context.SceneLoader == null: _context.SceneLoader = SceneLoader   // fill only when absent
      // caller owns seeding — NO InitFromGraph
  else:
      _context = new GameFlowContext { SceneLoader = SceneLoader }
      _context.InitFromGraph(_graph)                                         // unchanged path
  _runner = new BaseRunner(); Subscribe(); _running = true
  _runner.Start(_graph, _context, registry ?? new NodeExecutorRegistry())
```

## Validation / invariants

- **INV-1**: A provided context is the live `Context` the flow runs on; state seeded before boot is observed by
  flow actions and survives (not overwritten).
- **INV-2**: A provided context is NOT `InitFromGraph`-ed (a pre-seeded declared parameter keeps its value).
- **INV-3**: A provided context with a null scene loader gets the driver's; one with its own keeps it.
- **INV-4**: A provided registry's executor runs when the flow enters its node type; a null registry → empty
  registry (statement/await nodes still run).
- **INV-5**: `Boot()` (and `Boot(null, null)`) behave exactly as before: fresh context, scene loader set,
  `InitFromGraph`, empty registry. All prior guards fire for both forms.
- **INV-6**: graphcore/graphstandard untouched; 667 EditMode + 9 PlayMode stay green; gameflow 0.4.0 → 0.5.0.
