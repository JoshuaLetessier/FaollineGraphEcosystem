# Research: Break Hard Graph-to-Graph Asset References

All items below were open design questions not fully pinned down by the spec/prior discussion.
Each is resolved against the actual codebase (files read during research, not assumed).

## R1 — Does the public `TargetGraph` API need to change at all?

**Decision**: No. `GraphLinkNodeData.TargetGraph` keeps its exact signature — `public BaseGraph TargetGraph { get; set; }`. Only its backing storage changes, from a serialized `BaseGraph` field to a serialized `string` GUID field, with the property doing the GUID↔asset translation internally (Editor-only, see R2).

**Rationale**: `GraphLinkNodeView.cs` and `BaseNodeInspectorView.AddGraphLinkSection` (both Editor-assembly) are the *only* non-test call sites of `TargetGraph`, and both already treat it as an opaque `BaseGraph` getter/setter. Preserving the signature means **zero changes** are needed to either file — the drag-and-drop `ObjectField` binding, the canvas double-click navigation, and the "📎 Kind: Name" label all keep working unmodified. This is both the simplest path (Principle V) and the one with the smallest blast radius.

**Alternatives considered**: Expose only a raw string (`TargetGraphGuid`) and push GUID↔asset resolution into the two Editor call sites. Rejected — it would require editing three files instead of one for no behavioral gain, and scatters `AssetDatabase` calls across the codebase instead of keeping them behind one property.

## R2 — Where does `AssetDatabase` resolution live, given `GraphLinkNodeData` is in `Runtime/`?

**Decision**: Guard the `BaseGraph TargetGraph` property itself behind `#if UNITY_EDITOR`. The backing `string _targetGraphGuid` field is **not** guarded — it is a plain serialized string, present and readable in every build and in the raw `.asset` YAML.

**Rationale**: `UnityEditor.AssetDatabase` does not exist outside the editor, but nothing at runtime ever needs `TargetGraph` — `BaseRunner.EnterCurrentNode` (line 501) only checks `node is GraphLinkNodeData` and never touches the field, confirmed by reading the actual pass-through branch. This is exactly the distinction the original proposal's rejected "variante écartée" missed: that proposal wrapped the *field* in `#if UNITY_EDITOR`, which risks a built player silently losing the data during serialization stripping. Here the field always exists (verifiable by reading the `.asset` file, satisfying spec constraint about silent failure modes) — only the editor-convenience *accessor* is compiled out of players, which is inert anyway since it's never called there.

**Alternatives considered**: A `noEngineReferences`/separate-package split to keep `AssetDatabase` fully out of the Runtime assembly's compiled code even for Editor platforms. Rejected as over-engineering — `#if UNITY_EDITOR` inside a Runtime-assembly file is the ecosystem's existing convention elsewhere (`GraphLinkNodeView` already does equivalent Editor-only asset lookups) and needs no new assembly.

## R3 — Validator needs a raw-GUID accessor, not just the resolved property

**Decision**: Add a second public member, `string TargetGraphGuid { get; set; }`, alongside `TargetGraph`, not `#if`-guarded (plain string, available in Runtime too).

**Rationale**: Spec FR-009 requires the "unresolved reference" validator check to catch a **key-based** miss, not only a previously-resolved-then-broken reference. Since `TargetGraph` (Editor-only, GUID-backed) already returns `null` for both "never assigned" and "assigned but now unresolvable," `GraphValidator` needs the raw GUID to tell those two cases apart (empty GUID = never linked, fine; non-empty GUID + null resolve = broken, warn).

## R4 — `IGraphCatalog` shape: async via `Task`, or callback/event, mirroring `ISceneLoader`?

**Decision**: Callback-based, not `Task`/`async`:

```csharp
public interface IGraphCatalog
{
    void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
}
```

**Rationale**: `ISceneLoader.LoadScene` is a synchronous, fire-and-forget call whose completion is reported later through C# events (`AddressablesSceneLoader.SceneLoadCompleted`, `SceneLoadFailed`) — never `Task`/`async`. The constitution explicitly bans `UnityEvent` in favor of `Action<T>`, and the whole ecosystem (including `BaseRunner`) stays synchronous-call/async-callback, never `async`/`await`, so a `Task`-returning port would be the one inconsistent seam in the whole driver. A callback pair mirrors the existing idiom exactly and keeps `graphcore`/`graphgameflow` runtime code `async`-free.

**Alternatives considered**: `Task<BaseGraph> ResolveAsync(string graphId)`. Rejected — inconsistent with every other async seam in the ecosystem (scene loading, signal-based waits) and would need `async void` glue at every call site, which is a known footgun (unobserved exceptions) the codebase has otherwise avoided entirely.

## R5 — Zero-Addressables default implementation of `IGraphCatalog`

**Decision**: Ship a trivial `DirectGraphCatalog : IGraphCatalog` in `com.faolline.graphgameflow` (Runtime), backed by a `Dictionary<string, BaseGraph>` populated via `Register(graphId, graph)`, resolving synchronously (`onResolved` invoked immediately, same call stack).

**Rationale**: Spec FR-006 requires the resolution seam to work with zero asynchronous asset-loading technology installed, verified by automated tests with no such technology present (SC-006). Without a default implementation, `graphsave`'s restore path would have no seam to call in a non-Addressables project, forcing every consumer to hand-write the same five-line dictionary wrapper — exactly the ad hoc lookup table this lot exists to eliminate (spec background, User Story 2).

## R6 — Where does `GraphKeySourceRegistry`'s "mark as / see current keys" UI actually attach?

**Decision**: A dedicated `GraphKeyRegistryWindow` (`Faolline ▸ Graph ▸ Graph Key Registry` menu item, mirroring `GraphValidator`'s `Faolline ▸ Graph ▸ Validate Selected Graph` convention), not a per-field dropdown drawer.

**Rationale**: For scenes, `SceneKeySourceRegistry`'s consumer is `SceneNameFieldDrawer`, embedded in `LoadSceneActionEditor` — because a scene name is *typed by an author into a specific action's field*. Graphs have no equivalent authored field in this spec's scope: `BaseGraph.GraphId` (confirmed in `BaseGraph.cs`) is already a **stable GUID auto-assigned in `OnEnable`**, never author-typed, and Lot 4's `PreloadNextChapterAction` references its target via a direct `AssetReferenceT<BaseGraph>` picker (per the original request), not a raw string key field. So there is no "field" for a dropdown drawer to attach to — the registry's practical surface is a standalone window listing every `BaseGraph` asset in the project, its `GraphId`, and (per registered `IGraphKeySourceProvider`) whether it's currently promoted, with a "Mark as {ProviderLabel}" button per row. This keeps the registry/provider pattern identical to scenes (same `CanPromote`/`Promote`/`SourceLabel` shape) while adapting only the consuming UI to how graphs are actually authored.

**Alternatives considered**: Add a "Graph Key" foldout section to every graph asset's own custom inspector. Rejected for this lot — graph assets don't currently have a shared custom `Editor`, and adding one is a larger, unrelated surface change; a standalone window needs no new inspector and follows the `GraphValidator` precedent directly.

## R7 — `IGraphKeySourceProvider` needs one capability `ISceneKeySourceProvider` never needed

**Decision**: `IGraphKeySourceProvider` extends the mirrored shape with one addition:

```csharp
bool TryResolveGuid(string assetGuid, out string key);
```

**Rationale**: Spec FR-010 requires `GraphValidator` to flag a `SubGraphNodeData` whose target is itself a registered chapter-root graph. `ISceneKeySourceProvider` only exposes `GetKeys()` (a flat list of key strings) because no scene-side validator rule ever needed to go the other direction ("is *this specific* scene one of the registered ones?"). Confirmed by reading `SceneKeySourceRegistry.cs` and `AddressablesSceneKeyProvider.cs` in full — neither exposes a reverse lookup. `GraphValidator` runs synchronously at editor time on a specific `BaseGraph` asset with no live driver/`IGraphCatalog` instance necessarily registered, so the check must go through the editor-only provider registry (not through `IGraphCatalog`, which is a runtime, asynchronous seam with no guarantee anything is registered during a headless validation pass).

**Alternatives considered**: Reuse `IGraphCatalog` for this check (resolve the candidate's `GraphId` and see if it succeeds). Rejected — `IGraphCatalog` is async/callback-based and requires a project to have wired up a live instance; `GraphValidator` must produce a synchronous, deterministic answer in an EditMode test with no scene, no driver, and no registered catalog.

## R8 — Constitution Principle I: is the `_targetGraph` → `_targetGraphGuid` swap a violation?

**Decision**: Documented as a justified, explicit deviation (see `plan.md` Complexity Tracking), not silently absorbed.

**Rationale**: Per R1, the *public* C# API (`TargetGraph` property signature) is unchanged — the letter of "no public API may be removed without a deprecation cycle" is satisfied for the API surface. What does change, in the literal sense of "`BaseNodeData` fields are append-only," is the *private serialized field* backing one `BaseNodeData` subclass. Read strictly, that clause still covers this change. The requester (project lead, the constitution's own amendment authority) explicitly authorized skipping a deprecation cycle in conversation, on the grounds that no real project has serialized data against the old field — this is recorded as an explicit, reasoned deviation rather than treated as compliant by a technicality.

## R9 — FR-010's validator rule cannot live directly in `GraphValidator` as first drafted

**Problem found late** (during task breakdown, re-reading the constitution against the concrete design): `data-model.md`'s original draft of validator rule 2 has `GraphValidator` (in `com.faolline.graphcore`) call `GraphKeySourceRegistry`/`IGraphKeySourceProvider` (in `com.faolline.graphgameflow`) directly. That is a direct Constitution Principle II violation — "GraphCore MUST NOT reference `dialoguesystem`, `gameflow`, `questsystem`, or any other ecosystem lib — directly or transitively" — "chapter root" is a `gameflow` concept, not a universal graphcore one.

**Decision**: `graphcore` gains a small, generic extension seam instead, and the concrete "is this a registered chapter root" logic moves entirely into `graphgameflow`:

```csharp
// com.faolline.graphcore/Editor/Tools/ (graphcore — generic, no gameflow knowledge)
public interface IGraphValidatorExtension
{
    // Return a non-empty issue message if this extension considers `targetGraph` (the resolved
    // target of a hard SubGraphNodeData reference) problematic; null/empty = no opinion.
    string CheckSubGraphTarget(BaseGraph targetGraph);
}

public static class GraphValidatorExtensionRegistry
{
    public static void Register(IGraphValidatorExtension extension);
    public static void Unregister(IGraphValidatorExtension extension);
    public static IReadOnlyList<IGraphValidatorExtension> Extensions { get; }
}
```

`GraphValidator`'s `SubGraphNodeData` rule becomes: for each `SubGraphNodeData` with a resolved `TargetGraph`, call every registered extension's `CheckSubGraphTarget`; any non-empty result becomes a `Warning` issue. `graphcore` never learns what "chapter root" means — it just runs an opinion poll.

`com.faolline.graphgameflow.Editor` registers (via `[InitializeOnLoadMethod]`, same idiom as `AddressablesSceneKeyProvider.Register`) a `ChapterRootSubGraphValidatorExtension : IGraphValidatorExtension` whose `CheckSubGraphTarget` consults `GraphKeySourceRegistry.Providers` and calls each one's `TryResolveGuid`.

**Rationale**: This is not a new pattern invented for this feature — it is the *exact* seam the codebase already uses for the same shaped problem: the `ISceneKeySourceProvider` doc comment (read directly from `SceneKeySourceRegistry.cs`) states it "Mirrors graphcore's `ContextKeyLabelRegistry`/`IContextLabelResolver` seam" — i.e. graphcore already has precedent for "generic registry in graphcore, concrete resolver registered by the downstream lib that actually knows the domain concept" (also used previously for quest context labels per project history). `IGraphValidatorExtension` is the same shape applied to validation instead of labeling.

**Impact on task breakdown**: Lot 1 (graphcore) now additionally ships the generic seam itself; Lot 3 (graphgameflow, alongside `GraphKeySourceRegistry`) ships the concrete extension. This makes FR-010 a genuine cross-package task, unlike the rest of Lot 1 which is graphcore-only — reflected in `tasks.md` as a Foundational-phase task (the seam) plus a User-Story-3 task (the concrete extension), rather than folding both into User Story 1.

## R10 — Package version bumps

| Package | Current | New | Rationale |
|---|---|---|---|
| `com.faolline.graphcore` | 0.40.1 | 0.41.0 | New public member (`TargetGraphGuid`), new `GraphValidator` rules — additive, no public API removed (R1/R8) |
| `com.faolline.graphgameflow` | 0.16.1 | 0.17.0 | New `IGraphCatalog`/`DirectGraphCatalog`, new `GameFlowContext.GraphCatalog` property, new `IGraphKeySourceProvider`/`GraphKeySourceRegistry`/`GraphKeyRegistryWindow` — all additive |
| `com.faolline.graphgameflow.addressables` | 0.4.0 | 0.5.0 | New `AddressablesGraphCatalog`, `AddressablesGraphKeyProvider`, `PreloadNextChapterAction` — additive, mirrors existing scene adapter |
| `com.faolline.graphsave` | 0.8.0 | unchanged | Consumes the new seam (Lot 2) via existing `Restore(runner, graph, context)`; no code change required by this feature |
