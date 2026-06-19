# Quickstart — GraphLink cross-reference

## As a game author: associate a quest with a zone's flow

1. Open your zone's host graph (e.g. a `GameFlowGraph`) in its editor.
2. Add a **GraphLink** node and set its `TargetGraph` to the quest graph that belongs to this zone (and an
   optional `Note`, e.g. "side quest"). Leave it OFF the execution path — it is documentation, not a step.
3. The node shows up as "📎 Quest: Relics". Anyone opening this flow now reads which quests belong to the zone.
4. **Double-click** the GraphLink → the quest opens in the quest editor, that quest loaded.

> The GraphLink never runs. You can add/remove/re-point it freely; the game plays identically.

```csharp
// Code-first equivalent (off-path annotation):
var link = new GraphLinkNodeData {
    Id = "link_relics", NodeType = GraphLinkNodeData.NodeTypeId,
    TargetGraph = relicQuestGraph, Note = "side quest"
};
zoneFlowGraph.AddNode(link);   // not wired into any edge → inert
```

## As a lib author: make your graph openable from a GraphLink

Register your editor window once, so double-clicking a GraphLink that targets your graph type opens it:

```csharp
// com.faolline.<yourlib>/Editor/<YourLib>EditorRegistration.cs
using UnityEditor;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

internal static class YourLibEditorRegistration
{
    [InitializeOnLoadMethod]
    private static void Register() =>
        GraphEditorWindowRegistry.Register(typeof(YourGraph),
            g => YourGraphEditorWindow.Open((YourGraph)g));
}
```

If your lib does not register, a GraphLink targeting your graph still works — double-click falls back to
selecting/pinging the asset with a `[GraphCore]` diagnostic.

## Verify

- EditMode: run a flow with a GraphLink off-path and confirm the run is identical to without it; wire one
  on-path and confirm it passes straight through (no pause, same terminal state).
- Editor: register a fake opener for a graph type, call `GraphEditorWindowRegistry.Open(graph)`, confirm it
  fires; call `Open` for an unregistered type and confirm the graceful fallback + diagnostic (no throw).
