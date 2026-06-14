using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Concrete graph view for the GraphTest verification package.
    /// Hosts the canvas that displays <see cref="TestGraph"/> assets.
    /// </summary>
    public class TestGraphView : BaseGraphView
    {
        protected override BaseNodeView CreateNodeView(BaseNodeData node)
        {
            return node.NodeType switch
            {
                StartNodeData.NodeTypeId     => new StartNodeView((StartNodeData)node),
                TestStatementNodeData.NodeTypeId => new TestStatementNodeView((TestStatementNodeData)node),
                EndNodeData.NodeTypeId       => new EndNodeView((EndNodeData)node),
                ChoiceNodeData.NodeTypeId    => new ChoiceNodeView((ChoiceNodeData)node),
                SubGraphNodeData.NodeTypeId  => new SubGraphNodeView((SubGraphNodeData)node),
                _                            => null
            };
        }

        // ── Choice support ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the live <see cref="ChoiceNodeView"/> for the node with <paramref name="nodeId"/>,
        /// or null when no such choice view is on the canvas. Queried live from the canvas — no cache.
        /// </summary>
        public ChoiceNodeView GetChoiceView(string nodeId)
        {
            foreach (var n in nodes.ToList())
            {
                if (n is ChoiceNodeView cv && cv.NodeData != null && cv.NodeData.Id == nodeId)
                    return cv;
            }
            return null;
        }

        /// <summary>
        /// Removes every edge whose source is <paramref name="fromNodeId"/> and whose
        /// <c>PortName</c> equals <paramref name="portName"/> from both the graph data and the canvas.
        /// Used when a choice (and therefore its output port) is removed.
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

        protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge)
            => new TestEdgeView(edge);

        protected override void OnNodeCreated(BaseNodeData node)
        {
            // Automatically designate the first StartNodeData added as the graph entry point.
            if (node is StartNodeData && Graph != null && string.IsNullOrEmpty(Graph.EntryNodeId))
                Graph.EntryNodeId = node.Id;
        }

        public override void BuildContextualMenu(UnityEngine.UIElements.ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var mousePos = (Vector2)contentViewContainer.transform.matrix.inverse.MultiplyPoint(evt.localMousePosition);

            AppendAddStartAction(evt, mousePos);

            evt.menu.AppendAction("Add Statement Node", _ =>
            {
                var node = new TestStatementNodeData { NodeType = TestStatementNodeData.NodeTypeId };
                AddNodeToCanvas(node, mousePos);
            });

            evt.menu.AppendAction("Add End Node", _ =>
            {
                var node = new EndNodeData { NodeType = EndNodeData.NodeTypeId };
                AddNodeToCanvas(node, mousePos);
            });

            evt.menu.AppendAction("Add Choice Node", _ =>
            {
                var node = new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId };
                AddNodeToCanvas(node, mousePos);
            });

            evt.menu.AppendAction("Add SubGraph Node", _ =>
            {
                var node = new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId };
                AddNodeToCanvas(node, mousePos);
            });
        }
    }
}
