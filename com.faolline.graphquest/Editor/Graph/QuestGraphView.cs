using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Concrete canvas for <see cref="QuestGraph"/> assets: objectives as nodes, prerequisite edges between them
    /// (From→To = "To requires From"). The context menu adds an objective; there are no Start/End nodes (a quest is
    /// a reactive objective DAG, not a runner-walked graph). Not sealed — tests/extensions may subclass it.
    /// </summary>
    public class QuestGraphView : BaseGraphView
    {
        protected override BaseNodeView CreateNodeView(BaseNodeData node)
        {
            if (node == null || node.NodeType != ObjectiveNodeData.NodeTypeId) return null;
            // A quest has no Start/End node; flag the entry (no prerequisite) and terminal (no dependent) objectives
            // from the prerequisite topology so the view can mark them. The graph data carries all edges already.
            bool isEntry = !HasPrerequisite(Graph, node.Id);
            bool isTerminal = !HasDependent(Graph, node.Id);
            return new ObjectiveNodeView((ObjectiveNodeData)node, isEntry, isTerminal);
        }

        /// <summary>True when <paramref name="objectiveId"/> has a prerequisite edge (an incoming edge) — i.e. NOT an entry objective.</summary>
        public static bool HasPrerequisite(BaseGraph graph, string objectiveId)
        {
            if (graph?.Edges == null || string.IsNullOrEmpty(objectiveId)) return false;
            foreach (var e in graph.Edges)
                if (e != null && e.ToNodeId == objectiveId) return true;
            return false;
        }

        /// <summary>True when <paramref name="objectiveId"/> gates another objective (an outgoing edge) — i.e. NOT a terminal objective.</summary>
        public static bool HasDependent(BaseGraph graph, string objectiveId)
        {
            if (graph?.Edges == null || string.IsNullOrEmpty(objectiveId)) return false;
            foreach (var e in graph.Edges)
                if (e != null && e.FromNodeId == objectiveId) return true;
            return false;
        }

        protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => new QuestEdgeView(edge);

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            var mousePos = (Vector2)contentViewContainer.transform.matrix.inverse.MultiplyPoint(evt.localMousePosition);
            evt.menu.AppendAction("Add Objective Node", _ =>
                AddNodeToCanvas(new ObjectiveNodeData { NodeType = ObjectiveNodeData.NodeTypeId }, mousePos));
        }
    }
}
