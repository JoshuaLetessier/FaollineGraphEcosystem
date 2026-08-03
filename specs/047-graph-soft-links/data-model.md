# Data Model: Break Hard Graph-to-Graph Asset References

No new persisted save-data schema (no changes to `GraphRunSnapshot`). All entities below are
in-editor authoring data or runtime seam contracts.

> **Post-merge correction (graphgameflow 0.17.1):** the concrete extension this doc originally
> described, `ChapterRootSubGraphValidatorExtension`, was removed after review — it presumed that a
> graph registered via `GraphKeySourceRegistry` was necessarily meant to never be hard-referenced,
> which isn't a valid inference (a key can legitimately be registered purely for `IGraphCatalog`
> `GraphId` resolution, with no soft-loading intent at all). The generic `GraphValidatorExtensionRegistry`
> seam it plugged into remains in graphcore (empty by default); the sections below describing the
> removed extension are kept as historical record of what was built and then reverted, not current state.

## GraphLinkNodeData (modified)

`com.faolline.graphcore/Runtime/Nodes/GraphLinkNodeData.cs`

| Member | Before | After |
|---|---|---|
| Backing field | `[SerializeField] BaseGraph _targetGraph` | `[SerializeField] string _targetGraphGuid` |
| `TargetGraph` | `public BaseGraph TargetGraph { get; set; }` — direct field accessor | `public BaseGraph TargetGraph { get; set; }` — **unchanged signature**, `#if UNITY_EDITOR` only; get resolves `_targetGraphGuid` via `AssetDatabase.GUIDToAssetPath` + `LoadAssetAtPath<BaseGraph>`; set writes `_targetGraphGuid` via `AssetDatabase.GetAssetPath` + `AssetPathToGUID` |
| `TargetGraphGuid` *(new)* | — | `public string TargetGraphGuid { get; set; }` — plain string, no `#if` guard, available in Runtime and Editor; direct accessor to `_targetGraphGuid` |
| `Note` | unchanged | unchanged |

**Validation rules** (see Contracts): a non-empty `TargetGraphGuid` that does not resolve to an asset is a `GraphIssue` at `Warning` severity (consistent with the existing "isolated node" severity for this same non-executing node type).

**Relationships**: none tracked at the data-model level — a `GraphLinkNodeData` never appears in `AssetDatabase.GetDependencies` for its owning graph after this change (that is the feature's whole point, verified structurally, not via a new relationship object).

## SubGraphNodeData (unchanged)

`com.faolline.graphcore/Runtime/Nodes/SubGraphNodeData.cs` — no field, property, or behavior change. Included here only because it gains a *new validation rule that inspects it* (below); the node type itself is untouched per spec FR-003.

## IGraphCatalog (new port)

`com.faolline.graphgameflow/Runtime/Graph/IGraphCatalog.cs`

```csharp
public interface IGraphCatalog
{
    void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
}
```

Mirrors `ISceneLoader`'s role: the seam `GameFlowContext` holds and `LoadSceneAction`-equivalent callers use, never referencing a concrete loading technology.

## DirectGraphCatalog (new default implementation)

`com.faolline.graphgameflow/Runtime/Graph/DirectGraphCatalog.cs`

| Member | Description |
|---|---|
| `Register(string graphId, BaseGraph graph)` | Adds/replaces a direct in-memory mapping |
| `Unregister(string graphId)` | Removes a mapping |
| `Resolve(...)` | Synchronous: invokes `onResolved` immediately if `graphId` is registered, else `onFailed` |

Zero dependency on any asset-loading technology — the reference implementation proving FR-006/SC-006.

## GameFlowContext (modified)

`com.faolline.graphgameflow/Runtime/Context/GameFlowContext.cs`

| Member | Change |
|---|---|
| `GraphCatalog` *(new)* | `public IGraphCatalog GraphCatalog { get; set; }` — mirrors the existing `SceneLoader` property exactly: a runtime service field (Constitution VI: not a `bool`/`int`/`float`/`string` context parameter), carried across `DeepClone` as a shared reference (same treatment as `SceneLoader` in the existing `DeepClone` override) |

## IGraphKeySourceProvider (new, Editor-only)

`com.faolline.graphgameflow/Editor/Inspector/GraphKeySourceRegistry.cs`

```csharp
public interface IGraphKeySourceProvider
{
    string SourceLabel { get; }
    IReadOnlyList<string> GetKeys();
    bool CanPromote(string graphAssetPath, string graphId);
    void Promote(string graphAssetPath, string graphId);
    bool TryResolveGuid(string assetGuid, out string key);   // new vs. ISceneKeySourceProvider — see research.md R7
}
```

## GraphKeySourceRegistry (new, Editor-only)

Static registry of `IGraphKeySourceProvider`s — structurally identical to `SceneKeySourceRegistry` (`Register`/`Unregister`/`Providers`).

## GraphKeyRegistryWindow (new, Editor-only tool)

`com.faolline.graphgameflow/Editor/Tools/GraphKeyRegistryWindow.cs` — lists every `BaseGraph` asset in the project (via `AssetDatabase.FindAssets("t:BaseGraph")`), its `GraphId`, and per registered provider whether it currently resolves as one of that provider's keys (via `TryResolveGuid`), with a "Mark as {SourceLabel}" button calling `Promote(assetPath, graph.GraphId)`.

## IGraphValidatorExtension / GraphValidatorExtensionRegistry (new, graphcore, Editor-only)

`com.faolline.graphcore/Editor/Tools/GraphValidatorExtensionRegistry.cs` — a **generic** seam, added
specifically so `GraphValidator`'s new SubGraph rule (below) never has to know what a "chapter
root" is (Constitution Principle II forbids `graphcore` referencing `graphgameflow`; see
`research.md` R9 for why the originally-drafted direct call was wrong). Mirrors the ecosystem's
existing `ContextKeyLabelRegistry`/`IContextLabelResolver` seam shape (per `SceneKeySourceRegistry`'s
own doc comment, which cites that same precedent).

```csharp
public interface IGraphValidatorExtension
{
    // Non-empty return = this extension flags targetGraph as problematic; null/empty = no opinion.
    string CheckSubGraphTarget(BaseGraph targetGraph);
}

public static class GraphValidatorExtensionRegistry
{
    public static IReadOnlyList<IGraphValidatorExtension> Extensions { get; }
    public static void Register(IGraphValidatorExtension extension);
    public static void Unregister(IGraphValidatorExtension extension);
}
```

Empty by default (no extensions registered) — `graphcore` alone never flags anything through this
seam; only a downstream lib that registers one does.

## GraphValidator (modified)

`com.faolline.graphcore/Editor/Tools/GraphValidator.cs` — two additive rules (existing rules, severities, and report shape all unchanged):

1. **Unresolved GraphLink target** — for each `GraphLinkNodeData` with non-empty `TargetGraphGuid` and null `TargetGraph`: `Warning`, `"GraphLink '{Label}' references a target (GUID '{guid}') that no longer resolves to any asset."` Self-contained within `graphcore`, no extension seam needed.
2. **SubGraph crossing into a registered chapter root** — for each `SubGraphNodeData` with non-null `TargetGraph`: for each `GraphValidatorExtensionRegistry.Extensions`, call `CheckSubGraphTarget(sub.TargetGraph)`; any non-empty result becomes a `Warning` issue using that message verbatim. `graphcore` has zero knowledge of Addressables, chapters, or `GraphKeySourceRegistry` — it only runs the generic poll.

## ChapterRootSubGraphValidatorExtension (new, graphgameflow, Editor-only)

`com.faolline.graphgameflow/Editor/Tools/ChapterRootSubGraphValidatorExtension.cs` — the concrete
`IGraphValidatorExtension` that gives rule 2 (above) its actual meaning. Self-registers via
`[InitializeOnLoadMethod]` (same idiom as `AddressablesSceneKeyProvider.Register`).

```csharp
public sealed class ChapterRootSubGraphValidatorExtension : IGraphValidatorExtension
{
    public string CheckSubGraphTarget(BaseGraph targetGraph)
    {
        // resolve targetGraph's asset GUID, ask every GraphKeySourceRegistry.Providers entry's
        // TryResolveGuid; on a hit, return the warning message (spec FR-010's wording); else null.
    }

    [InitializeOnLoadMethod]
    private static void Register() => GraphValidatorExtensionRegistry.Register(new ChapterRootSubGraphValidatorExtension());
}
```

## AddressablesGraphCatalog (new, in `com.faolline.graphgameflow.addressables`)

`com.faolline.graphgameflow.addressables/Runtime/AddressablesGraphCatalog.cs` — `IGraphCatalog` implementation resolving `graphId` to an Addressable address/key, loading the `BaseGraph` asset via `Addressables.LoadAssetAsync<BaseGraph>`, invoking `onResolved`/`onFailed` on completion. Mirrors `AddressablesSceneLoader`'s async-operation-polling shape (own coroutine/callback wiring, not `Task`).

## AddressablesGraphKeyProvider (new, in `com.faolline.graphgameflow.addressables`, Editor-only)

`com.faolline.graphgameflow.addressables/Editor/AddressablesGraphKeyProvider.cs` — `IGraphKeySourceProvider` mirroring `AddressablesSceneKeyProvider` exactly (same `[InitializeOnLoadMethod]` self-registration), `SourceLabel => "Addressable"`, `GetKeys()` filtered to entries whose asset type is a `BaseGraph` subclass instead of `SceneAsset`, `TryResolveGuid` cross-referencing `AddressableAssetSettings` entries by GUID.

## PreloadNextChapterAction (new, in `com.faolline.graphgameflow.addressables`)

`com.faolline.graphgameflow.addressables/Runtime/PreloadNextChapterAction.cs` — `BaseAction` subclass:

| Member | Description |
|---|---|
| `_nextChapter` | `[SerializeField] AssetReferenceT<BaseGraph>` — soft reference, own Addressables-provided drawer, no build-time dependency |
| `Execute(BaseContext context)` | Triggers `Addressables.LoadAssetAsync<BaseGraph>(_nextChapter)`; on completion, stores the resolved `BaseGraph` somewhere the host can retrieve it (a `GameFlowContext`-scoped field, mirroring how `AddressablesSceneLoader` reports via events rather than a return value) and optionally raises a configurable completion `SignalDef`, mirroring `AddressablesSceneLoader`'s `_loadCompletedSignal`/`_loadFailedSignal` pattern for the two usage forms in spec User Story 4 (early-trigger-then-`OnEnded`-reboot, or park-on-signal via existing `AwaitSignalNames`) |
