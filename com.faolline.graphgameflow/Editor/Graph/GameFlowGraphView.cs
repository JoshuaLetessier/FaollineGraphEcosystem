using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// The gameflow canvas. Hosts <see cref="GameFlowGraph"/> assets and maps the universal node types to
    /// their gameflow node views; the right-click menu adds the universal node set.
    /// </summary>
    public class GameFlowGraphView : BaseGraphView
    {
        protected override BaseNodeView CreateNodeView(BaseNodeData node)
        {
            return node.NodeType switch
            {
                StartNodeData.NodeTypeId     => new StartNodeView((StartNodeData)node),
                StatementNodeData.NodeTypeId => new StatementNodeView((StatementNodeData)node),
                EndNodeData.NodeTypeId       => new EndNodeView((EndNodeData)node),
                ChoiceNodeData.NodeTypeId    => new ChoiceNodeView((ChoiceNodeData)node),
                SubGraphNodeData.NodeTypeId  => new SubGraphNodeView((SubGraphNodeData)node),
                _                            => null
            };
        }

        protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge)
            => new GameFlowEdgeView(edge);

        protected override void OnNodeCreated(BaseNodeData node)
        {
            // The first Start node added becomes the graph entry point.
            if (node is StartNodeData && Graph != null && string.IsNullOrEmpty(Graph.EntryNodeId))
                Graph.EntryNodeId = node.Id;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var mousePos = (Vector2)contentViewContainer.transform.matrix.inverse.MultiplyPoint(evt.localMousePosition);

            evt.menu.AppendAction("Add Start Node", _ =>
                AddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, mousePos));
            evt.menu.AppendAction("Add Statement Node", _ =>
                AddNodeToCanvas(new StatementNodeData { NodeType = StatementNodeData.NodeTypeId }, mousePos));
            evt.menu.AppendAction("Add Choice Node", _ =>
                AddNodeToCanvas(new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId }, mousePos));
            evt.menu.AppendAction("Add SubGraph Node", _ =>
                AddNodeToCanvas(new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId }, mousePos));
            evt.menu.AppendAction("Add End Node", _ =>
                AddNodeToCanvas(new EndNodeData { NodeType = EndNodeData.NodeTypeId }, mousePos));
        }

        // ── Choice support (used by the inspector when choices change) ────────────

        /// <summary>The live <see cref="ChoiceNodeView"/> for <paramref name="nodeId"/>, or null.</summary>
        public ChoiceNodeView GetChoiceView(string nodeId)
        {
            foreach (var n in nodes.ToList())
                if (n is ChoiceNodeView cv && cv.NodeData != null && cv.NodeData.Id == nodeId)
                    return cv;
            return null;
        }

        /// <summary>
        /// Removes every edge whose source is <paramref name="fromNodeId"/> with the given
        /// <paramref name="portName"/>, from both the graph data and the canvas (used when a choice is removed).
        /// </summary>
        public void RemoveChoiceEdges(string fromNodeId, string portName)
        {
            if (Graph != null)
            {
                var dataToRemove = new List<BaseEdgeData>();
                foreach (var e in Graph.Edges)
                    if (e.FromNodeId == fromNodeId && e.PortName == portName)
                        dataToRemove.Add(e);
                foreach (var e in dataToRemove)
                    Graph.RemoveEdge(e);
            }

            var viewsToRemove = new List<Edge>();
            foreach (var el in edges.ToList())
            {
                if (el is BaseEdgeView bev && bev.EdgeData != null
                    && bev.EdgeData.FromNodeId == fromNodeId && bev.EdgeData.PortName == portName)
                    viewsToRemove.Add(el);
            }
            foreach (var ev in viewsToRemove)
            {
                ev.input?.Disconnect(ev);
                ev.output?.Disconnect(ev);
                RemoveElement(ev);
            }
        }
    }
}
