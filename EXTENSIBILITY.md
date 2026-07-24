# Extensibility — the `InspectorExtensionRegistry` seam

`graphcore` owns the graph inspector, but it doesn't know about `graphlocalization`, `graphquest`, or
any other downstream lib. `InspectorExtensionRegistry`
(`com.faolline.graphcore/Editor/Registry/InspectorExtensionRegistry.cs`) is the seam that lets a
downstream lib inject its own UI into that inspector without graphcore taking a compile-time dependency
on it — consistent with the [tier rules](ARCHITECTURE.md) (T1+ never references its siblings, and
graphcore never references anything above it).

This document is the seam's reference: its contract, and `GraphCategoryGroup` walked end-to-end as a
fully worked example. If you're adding inspector UI from your own package, start here.

## The contract

```csharp
public static class InspectorExtensionRegistry
{
    public delegate void NodeSectionDelegate(BaseNodeData node, VisualElement parent, BaseGraph graph, Action markDirty);
    public delegate void GraphSectionDelegate(BaseGraph graph, VisualElement parent, Action markDirty);

    public static void RegisterNodeSection(NodeSectionDelegate callback);
    public static void RegisterGraphSection(GraphSectionDelegate callback);
}
```

- **`RegisterNodeSection`** — called once per selected node, while a node's inspector is being built.
  Your callback receives the node, the `VisualElement` to append UI to, the owning graph (for context),
  and a `markDirty` action.
- **`RegisterGraphSection`** — called when nothing is selected (the graph-level, "no selection" panel).
  Your callback receives the graph, the parent `VisualElement`, and `markDirty`.
- **Call ordering** — sections run in registration order (`_nodeSections`/`_graphSections` are plain
  `List<T>`s appended to on register), so display order follows load/registration order. Don't rely on
  a specific position relative to another lib's extension.
- **Idempotent registration** — both `Register*` methods no-op if the callback is already present, so a
  domain-reload-triggered re-registration never double-adds your section.
- **`markDirty`** — a callback the inspector supplies to mark *the graph being inspected* dirty after an
  edit. It only covers that one asset. If your extension edits something else — a companion asset, a
  different `ScriptableObject` your data lives on — call `EditorUtility.SetDirty(...)` on **that** asset
  yourself; don't expect `markDirty` to cover it. `GraphCategoryGroupInspectorExtension` below is exactly
  that case.

## Registering an extension

Follow the same shape for every extension: a static class, registered once via `[InitializeOnLoad]`.

```csharp
[InitializeOnLoad]
public static class MyInspectorExtension
{
    static MyInspectorExtension()
    {
        InspectorExtensionRegistry.RegisterGraphSection(BuildGraphSection);
    }

    private static void BuildGraphSection(BaseGraph graph, VisualElement parent, Action markDirty)
    {
        // build + parent.Add(...)
    }
}
```

## Two shapes of extension

| | Edits data on the inspected graph | Edits a foreign asset |
|---|---|---|
| Example | `LocalizationInspectorExtension` (`com.faolline.graphlocalization/Editor/`) | `GraphCategoryGroupInspectorExtension` (`com.faolline.graphcore/Editor/Grouping/`) |
| What it reads | `graph` itself, cast to a marker interface (`ILocalizedGraph`) | Project-wide scan for a *different* asset type that references `graph` |
| Dirtying | `EditorUtility.SetDirty(graph)` — the graph passed in | `EditorUtility.SetDirty(group)` — the foreign asset, not `graph` |
| Reversibility | N/A (in-place field edit) | `Undo.RecordObject(group, ...)` before mutating, same as any other asset edit |

The first shape is the simpler, more common case: the extension only ever touches the graph it was
handed. The second is the same seam turned to a different job — organizing graphs from *outside* any
one graph — and is the one worth studying if your data doesn't live on the graph itself.

## Worked example: `GraphCategoryGroup`

**The asset** (`com.faolline.graphcore/Runtime/Grouping/GraphCategoryGroup.cs`) is a concrete,
`[CreateAssetMenu]`-exposed `ScriptableObject`: a label plus a `List<BaseGraph>`. No code required to
create one — `Create > Faolline > Graph Category Group` in the Project window. A graph can belong to
any number of groups at once (this is intentional: "Main" and "Chapter 1" aren't mutually exclusive).

It deliberately carries **no stable-GUID identity** the way `VariableDef`/`SignalDef`/`CollectionDef`
do — those earn that machinery because the runtime looks them up by key. `GraphCategoryGroup` has zero
runtime consumers; it's pure editor-time organizational metadata, so it stays as simple as a label and a
list.

**The extension** (`com.faolline.graphcore/Editor/Grouping/GraphCategoryGroupInspectorExtension.cs`)
registers a graph section that:

1. **Reverse-scans** `AssetDatabase.FindAssets("t:GraphCategoryGroup")` for every group that
   `Contains(graph)`, since membership is stored forward-only (group → graphs). This scan runs once per
   inspector bind (i.e. once per selection change, driven by `BaseNodeInspectorView.BuildNoSelectionContent`)
   — not once per redraw — so it stays cheap even as the number of groups grows.
2. Lists each matching group with a **Remove** button.
3. Offers an `ObjectField` + **Add To Group** button to add the currently-inspected graph to any group
   asset dropped in.

Both mutations go through `Undo.RecordObject` + `EditorUtility.SetDirty` on the **group**, not the
graph — the graph being inspected isn't the asset that changed.

## What this doesn't cover

`GraphCategoryGroup` solves the generic case: organize any `BaseGraph` into named, possibly-overlapping
buckets. It does **not** cover per-project needs like a group hierarchy, colors, or custom filtering —
those stay out of graphcore on purpose. `InspectorExtensionRegistry` is the open extension point for
them: register your own `GraphSectionDelegate` following the pattern above rather than requesting those
features on `GraphCategoryGroup` itself.

## Checklist for a new extension

1. Decide which shape you need (edits the graph itself, or a foreign asset) — see the table above.
2. Register in a static class via `[InitializeOnLoad]`, in your package's `Editor` assembly.
3. If you mutate a foreign asset, dirty and (if relevant) `Undo.RecordObject` that asset directly —
   don't rely on `markDirty`.
4. Don't assume a display position relative to other extensions.
5. Keep any per-bind scans (`AssetDatabase.FindAssets` or similar) out of per-frame code paths — they
   should run once per selection/bind, matching how `RegisterGraphSection`/`RegisterNodeSection`
   callbacks are actually invoked.
