# Quickstart: GraphCore Editor Layer

**Branch**: `003-editor-layer` | **Date**: 2026-05-28

This guide shows a downstream lib how to build a working graph editor window on top of
`com.faolline.graphcore` in three steps.

---

## Prerequisites

- `com.faolline.graphcore` 0.3.0+ installed (Runtime + Editor assemblies)
- Your lib's asmdef references both `com.faolline.graphcore.Runtime` and
  `com.faolline.graphcore.Editor` (Editor only)

---

## Step 1 — Register a Node Type Color

In your lib's editor assembly, register a color for each custom node type you want to
appear with a distinct background in the canvas.

```csharp
// MyLib/Editor/MyLibEditorStartup.cs
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore.Editor;

[InitializeOnLoad]
public static class MyLibEditorStartup
{
    static MyLibEditorStartup()
    {
        NodeTypeColorRegistry.Register("mylib/dialogue", new Color(0.2f, 0.4f, 0.8f));
        NodeTypeColorRegistry.Register("mylib/choice",   new Color(0.8f, 0.5f, 0.1f));
    }
}
```

Colors registered here are applied automatically when any `BaseNodeView` subclass calls
`ResolveColor()` during canvas construction.

---

## Step 2 — Implement BaseNodeView and BaseEdgeView

Create concrete view classes for your node and edge types.

```csharp
// MyLib/Editor/Views/DialogueNodeView.cs
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using UnityEngine;

public class DialogueNodeView : BaseNodeView
{
    protected override bool HasColorOverride => NodeData.HasColorOverride;
    protected override Color ColorOverride   => NodeData.NodeColor;

    protected override void OnBuildView()
    {
        title = "Dialogue";
        // Add ports, labels, or custom fields here
    }
}
```

```csharp
// MyLib/Editor/Views/MyEdgeView.cs
using Faolline.GraphCore.Editor;

public class MyEdgeView : BaseEdgeView
{
    // No color override — use type color from NodeTypeColorRegistry
}
```

---

## Step 3 — Implement BaseGraphView and BaseGraphEditorWindow

```csharp
// MyLib/Editor/MyGraphView.cs
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

public class MyGraphView : BaseGraphView
{
    protected override BaseNodeView CreateNodeView(BaseNodeData node)
    {
        // Return the right view subclass per node type
        return node.NodeType switch
        {
            "mylib/dialogue" => new DialogueNodeView { NodeData = node },
            _                => new GenericNodeView  { NodeData = node }
        };
    }

    protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge)
    {
        return new MyEdgeView { EdgeData = edge };
    }

    // Optional: react to authoring events
    protected override void OnNodeCreated(BaseNodeData node)
    {
        base.OnNodeCreated(node);
        Debug.Log($"[MyLib] Node created: {node.Id}");
    }
}
```

```csharp
// MyLib/Editor/MyGraphEditorWindow.cs
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using UnityEditor;
using UnityEngine;

public class MyGraphEditorWindow : BaseGraphEditorWindow
{
    [MenuItem("MyLib/Open Graph Editor")]
    public static void Open()
    {
        var window = GetWindow<MyGraphEditorWindow>("My Graph Editor");
        window.Show();
    }

    // Called from your custom asset inspector double-click:
    public static void Open(BaseGraph graph)
    {
        var window = GetWindow<MyGraphEditorWindow>("My Graph Editor");
        window.LoadGraph(graph);
        window.Show();
    }

    protected override BaseGraphView CreateGraphView() => new MyGraphView();
}
```

---

## What You Get Out of the Box

| Feature | Provided by graphcore | Your lib provides |
|---------|----------------------|-------------------|
| Canvas pan / zoom | ✅ | — |
| Node creation (right-click menu) | ✅ (base nodes) | Custom menu entries |
| Edge connect / disconnect | ✅ | — |
| Cycle detection on every connect | ✅ | — |
| Copy / paste with new GUIDs | ✅ | — |
| Save (Ctrl+S / toolbar button) | ✅ | — |
| Node color resolution chain | ✅ | `Register` + optional override |
| Node content area layout | — | `OnBuildView()` |
| Custom context menu items | — | Override `BuildContextualMenu` |

---

## USS Customization

To restyle the canvas or nodes, add a USS file in your lib's Editor assembly and load it
in your `BaseGraphView` subclass constructor:

```csharp
public MyGraphView()
{
    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        "Packages/com.mylib.dialoguesystem/Editor/Resources/MyGraph.uss");
    styleSheets.Add(sheet);
}
```

Do **not** assign styles via `style.backgroundColor = ...` or any inline C# property —
all visual styling must go through USS.
