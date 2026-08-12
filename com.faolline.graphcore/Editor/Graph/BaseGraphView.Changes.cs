using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Faolline.GraphLogging;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
                HandleRemovals(change.elementsToRemove);

            if (change.edgesToCreate != null)
                HandleEdgeCreation(change.edgesToCreate);

            return change;
        }

        /// <summary>
        /// Test hook: simulates GraphView removing <paramref name="elements"/> (the same path the
        /// canvas uses when the user presses Delete). Mutates the list like the real change pipeline.
        /// </summary>
        public void HandleRemovalsForTest(List<GraphElement> elements) => HandleRemovals(elements);

        private void HandleRemovals(List<GraphElement> elements)
        {
            var protectedByGroup = new System.Collections.Generic.HashSet<string>();
            foreach (var el in elements)
                if (el is BaseGroupView gv && gv.GroupData != null)
                    foreach (var id in gv.GroupData.NodeIds)
                        protectedByGroup.Add(id);

            if (protectedByGroup.Count > 0)
            {
                elements.RemoveAll(el =>
                    el is BaseNodeView nv && nv.NodeData != null && protectedByGroup.Contains(nv.NodeData.Id));
            }

            foreach (var element in elements)
            {
                if (element is BaseNodeView nodeView && nodeView.NodeData != null)
                {
                    var nodeData = nodeView.NodeData;

                    if (_graph != null)
                    {
                        var edgeList = new List<BaseEdgeData>();
                        foreach (var e in _graph.Edges)
                        {
                            if (e.FromNodeId == nodeData.Id || e.ToNodeId == nodeData.Id)
                                edgeList.Add(e);
                        }
                        foreach (var e in edgeList)
                            _graph.RemoveEdge(e);

                        _graph.RemoveNode(nodeData);
                    }

                    _nodeViews.Remove(nodeData.Id);
                    _isDirty = true;
                    OnNodeDeleted(nodeData);
                }
                else if (element is BaseEdgeView edgeView && edgeView.EdgeData != null)
                {
                    _graph?.RemoveEdge(edgeView.EdgeData);
                    _isDirty = true;
                }
                else if (element is BaseGroupView groupView && groupView.GroupData != null)
                {
                    _graph?.RemoveGroup(groupView.GroupData);
                    _groupViews.Remove(groupView.GroupData.Id);
                    _isDirty = true;
                }
            }
        }

        private void HandleEdgeCreation(List<Edge> edges)
        {
            foreach (var edge in edges)
            {
                if (edge is BaseEdgeView edgeView)
                {
                    BaseNodeData fromNode = null;
                    BaseNodeData toNode = null;

                    if (edge.output?.node is BaseNodeView outNode)
                        fromNode = outNode.NodeData;
                    if (edge.input?.node is BaseNodeView inNode)
                        toNode = inNode.NodeData;

                    if (fromNode == null || toNode == null) continue;

                    var edgeData = new BaseEdgeData();
                    edgeData.Id = System.Guid.NewGuid().ToString("D");
                    edgeData.FromNodeId = fromNode.Id;
                    edgeData.ToNodeId = toNode.Id;
                    edgeData.PortName = edge.output?.portName ?? string.Empty;

                    BaseGraph targetGraph = null;
                    if (toNode is SubGraphNodeData subNode)
                        targetGraph = subNode.TargetGraph;

                    var cycleResult = CycleDetector.Check(_graph, targetGraph);
                    if (cycleResult.HasCycle)
                    {
                        var path = string.Join(" → ", cycleResult.CyclePath);
                        Logging.Error("GraphCore", $"[GraphCore] Cycle detected: {path}");
                        edges.Remove(edge);
                        continue;
                    }

                    edgeView.EdgeData = edgeData;
                    _graph?.AddEdge(edgeData);
                    _isDirty = true;
                    OnEdgeConnected(edgeData);
                }
            }
        }
    }
}
