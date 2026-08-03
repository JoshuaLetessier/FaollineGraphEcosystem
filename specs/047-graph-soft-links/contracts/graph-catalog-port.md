# Contract: IGraphCatalog port + editor key registry (Lots 2-3 — graphgameflow)

**Package**: `com.faolline.graphgameflow` 0.16.1 → 0.17.1

> **Post-merge correction (0.17.1):** `ChapterRootSubGraphValidatorExtension`, described below, was
> removed — it presumed a registered key always implies "never hard-reference this graph," which
> isn't a valid inference (a key can be registered purely for `IGraphCatalog` resolution with no
> soft-loading intent). Kept here as historical record; it is no longer part of this package.

## Runtime API surface (Lot 2)

```csharp
namespace Faolline.GraphGameFlow
{
    public interface IGraphCatalog
    {
        /// Resolves graphId to a BaseGraph asynchronously. Exactly one of onResolved/onFailed
        /// fires, exactly once, per call — mirrors ISceneLoader's callback idiom (no Task/async).
        void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
    }

    /// Zero-dependency default: synchronous, in-memory. Proves FR-006 (works with no
    /// asynchronous asset-loading technology installed).
    public class DirectGraphCatalog : IGraphCatalog
    {
        public void Register(string graphId, BaseGraph graph);
        public void Unregister(string graphId);
        public void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
    }

    public class GameFlowContext : BaseContext
    {
        public IGraphCatalog GraphCatalog { get; set; }   // NEW — mirrors SceneLoader exactly
        // ... existing members unchanged
    }
}
```

## Editor API surface (Lot 3)

```csharp
namespace Faolline.GraphGameFlow.Editor
{
    public interface IGraphKeySourceProvider
    {
        string SourceLabel { get; }
        IReadOnlyList<string> GetKeys();
        bool CanPromote(string graphAssetPath, string graphId);
        void Promote(string graphAssetPath, string graphId);
        bool TryResolveGuid(string assetGuid, out string key);   // NEW vs. ISceneKeySourceProvider
    }

    public static class GraphKeySourceRegistry
    {
        public static IReadOnlyList<IGraphKeySourceProvider> Providers { get; }
        public static void Register(IGraphKeySourceProvider provider);
        public static void Unregister(IGraphKeySourceProvider provider);
    }
}
```

`GraphKeyRegistryWindow` (menu: `Faolline ▸ Graph ▸ Graph Key Registry`) is one consumer of
`GraphKeySourceRegistry` in this lot — see `data-model.md`. The other is
`ChapterRootSubGraphValidatorExtension`:

```csharp
namespace Faolline.GraphGameFlow.Editor
{
    // Registers into graphcore's GraphValidatorExtensionRegistry (see graphlink-soft-reference.md)
    // — this is how FR-010's rule gets its "chapter root" meaning without graphcore ever
    // referencing graphgameflow (Constitution Principle II; see research.md R9).
    public sealed class ChapterRootSubGraphValidatorExtension : IGraphValidatorExtension
    {
        public string CheckSubGraphTarget(BaseGraph targetGraph); // consults GraphKeySourceRegistry.Providers
    }
}
```

## Compatibility contract

- Purely additive: no existing public member of `GameFlowContext`, `ISceneLoader`, or any other
  existing type changes shape. `GraphCatalog` defaults to `null` (mirroring `SceneLoader`'s own
  boot-time-assigned default) — existing consumers who never touch it are unaffected.
- `graphsave`'s `GraphRunSnapshot.Restore(runner, graph, context)` signature is **unchanged** —
  this lot gives the *caller* a way to obtain `graph` from `GraphId` before calling `Restore`; it
  does not touch `com.faolline.graphsave` at all (spec Assumptions).

## Behavioral contract

- `IGraphCatalog.Resolve` MUST invoke exactly one of `onResolved`/`onFailed`, exactly once, for
  every call — no double-invocation, no silent no-op (spec Edge Cases: "resolution must fail in a
  way the caller can detect").
- `DirectGraphCatalog.Resolve` for an unregistered `graphId` MUST call `onFailed`, never return a
  null `BaseGraph` via `onResolved` (spec Edge Cases).
