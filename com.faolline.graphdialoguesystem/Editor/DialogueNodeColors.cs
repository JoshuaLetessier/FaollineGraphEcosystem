using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Registers a distinct, high-contrast display color per dialogue node type (accessibility:
    /// node types are recognizable by color as well as by title). Uses graphcore's existing
    /// <see cref="NodeTypeColorRegistry"/> pipeline — no inline styling, no graphcore change.
    /// Colors are dark, saturated hues that keep the light node title/labels readable.
    /// </summary>
    [InitializeOnLoad]
    public static class DialogueNodeColors
    {
        static DialogueNodeColors()
        {
            NodeTypeColorRegistry.Register(StartNodeData.NodeTypeId,        new Color(0.16f, 0.40f, 0.18f)); // green
            NodeTypeColorRegistry.Register(DialogueLineNodeData.NodeTypeId, new Color(0.16f, 0.28f, 0.45f)); // blue
            NodeTypeColorRegistry.Register(ChoiceNodeData.NodeTypeId,       new Color(0.45f, 0.34f, 0.12f)); // amber
            NodeTypeColorRegistry.Register(SubGraphNodeData.NodeTypeId,     new Color(0.34f, 0.20f, 0.45f)); // purple
            NodeTypeColorRegistry.Register(EndNodeData.NodeTypeId,          new Color(0.45f, 0.18f, 0.18f)); // red
        }
    }
}
