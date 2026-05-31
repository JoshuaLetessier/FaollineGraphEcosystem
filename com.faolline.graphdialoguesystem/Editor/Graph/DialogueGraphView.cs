using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Concrete graph view for dialogue graphs. Dispatches node views in <see cref="CreateNodeView"/>,
    /// adds each node type to the context menu, and provides choice-port helpers. Reuses graphcore's
    /// LoadGraph data-safety and <c>ReconnectNodeEdges</c>.
    /// </summary>
    public class DialogueGraphView : BaseGraphView
    {
        protected override BaseNodeView CreateNodeView(BaseNodeData node)
        {
            return node.NodeType switch
            {
                StartNodeData.NodeTypeId        => new StartNodeView((StartNodeData)node),
                DialogueLineNodeData.NodeTypeId => new DialogueLineNodeView((DialogueLineNodeData)node),
                EndNodeData.NodeTypeId          => new EndNodeView((EndNodeData)node),
                ChoiceNodeData.NodeTypeId       => new ChoiceNodeView((ChoiceNodeData)node),
                SubGraphNodeData.NodeTypeId     => new SubGraphNodeView((SubGraphNodeData)node),
                _                               => null
            };
        }

        protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => new DialogueEdgeView(edge);

        // ── Edge selection → inspector wiring ──────────────────────────────────
        // graphcore's BaseGraphView surfaces node selection (OnNodeSelected / OnSelectionCleared).
        // We extend the same overridden selection hooks to also surface a single selected edge, so
        // the inspector can edit that connection's gating condition (FR-021). No graphcore change.

        /// <summary>Raised when exactly one edge is selected on the canvas.</summary>
        public System.Action<BaseEdgeData> OnEdgeSelected;

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifyEdgeSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifyEdgeSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifyEdgeSelection();
        }

        private void NotifyEdgeSelection()
        {
            if (selection.Count == 1 && selection[0] is BaseEdgeView edgeView && edgeView.EdgeData != null)
                OnEdgeSelected?.Invoke(edgeView.EdgeData);
        }

        // ── Choice support ────────────────────────────────────────────────────

        /// <summary>Returns the live <see cref="ChoiceNodeView"/> for <paramref name="nodeId"/>, or null.</summary>
        public ChoiceNodeView GetChoiceView(string nodeId)
        {
            foreach (var n in nodes.ToList())
                if (n is ChoiceNodeView cv && cv.NodeData != null && cv.NodeData.Id == nodeId)
                    return cv;
            return null;
        }

        /// <summary>
        /// Removes every edge whose source is <paramref name="fromNodeId"/> and whose <c>PortName</c>
        /// equals <paramref name="portName"/> from both the graph data and the canvas. Used when a
        /// choice (and therefore its output port) is removed.
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

        protected override void OnNodeCreated(BaseNodeData node)
        {
            // Automatically designate the first StartNodeData added as the graph entry point.
            if (node is StartNodeData && Graph != null && string.IsNullOrEmpty(Graph.EntryNodeId))
                Graph.EntryNodeId = node.Id;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var mousePos = (Vector2)contentViewContainer.transform.matrix.inverse.MultiplyPoint(evt.localMousePosition);

            evt.menu.AppendAction("Add Start Node", _ =>
                AddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, mousePos));

            evt.menu.AppendAction("Add Line Node", _ =>
                AddNodeToCanvas(new DialogueLineNodeData { NodeType = DialogueLineNodeData.NodeTypeId }, mousePos));

            evt.menu.AppendAction("Add Choice Node", _ =>
                AddNodeToCanvas(new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId }, mousePos));

            evt.menu.AppendAction("Add SubDialogue Node", _ =>
                AddNodeToCanvas(new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId }, mousePos));

            evt.menu.AppendAction("Add End Node", _ =>
                AddNodeToCanvas(new EndNodeData { NodeType = EndNodeData.NodeTypeId }, mousePos));
        }
    }
}
