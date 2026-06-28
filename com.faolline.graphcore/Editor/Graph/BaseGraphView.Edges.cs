using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        /// <summary>
        /// Rebuilds and reconnects every edge view touching <paramref name="nodeId"/> from the graph
        /// data. Call after a node view regenerates its ports (e.g. a Choice node adding/removing a
        /// choice) so edges bound to surviving ports are reconnected rather than left orphaned.
        /// </summary>
        public void ReconnectNodeEdges(string nodeId)
        {
            if (_graph == null) return;
            if (!_nodeViews.ContainsKey(nodeId)) return;

            var stale = new List<Edge>();
            foreach (var el in edges.ToList())
            {
                if (el is BaseEdgeView bev && bev.EdgeData != null
                    && (bev.EdgeData.FromNodeId == nodeId || bev.EdgeData.ToNodeId == nodeId))
                    stale.Add(el);
            }
            foreach (var e in stale)
            {
                e.output?.Disconnect(e);
                e.input?.Disconnect(e);
                RemoveElement(e);
            }

            foreach (var edgeData in _graph.Edges)
            {
                if (edgeData.FromNodeId != nodeId && edgeData.ToNodeId != nodeId) continue;
                var view = CreateEdgeView(edgeData);
                if (view == null) continue;
                ConnectEdgeView(view, edgeData);
                AddElement(view);
            }
        }

        /// <summary>
        /// Reconnects a reloaded <paramref name="edgeView"/> to the source/target node ports so it
        /// renders on the canvas and tracks node movement. The source port is matched by
        /// <see cref="BaseEdgeData.PortName"/> (which equals the choice Id for Choice nodes); the
        /// target uses the node's first input port. No-op if either endpoint cannot be resolved.
        /// </summary>
        private void ConnectEdgeView(BaseEdgeView edgeView, BaseEdgeData edgeData)
        {
            if (edgeData == null) return;
            if (!_nodeViews.TryGetValue(edgeData.FromNodeId, out var fromView)) return;
            if (!_nodeViews.TryGetValue(edgeData.ToNodeId, out var toView)) return;

            var outputPort = FindPort(fromView.outputContainer, edgeData.PortName);
            var inputPort  = FindPort(toView.inputContainer, null);
            if (outputPort == null || inputPort == null) return;

            edgeView.output = outputPort;
            edgeView.input  = inputPort;
            outputPort.Connect(edgeView);
            inputPort.Connect(edgeView);
        }

        /// <summary>
        /// Returns the port in <paramref name="container"/> whose <c>portName</c> equals
        /// <paramref name="portName"/>. When <paramref name="portName"/> is null/empty, returns the
        /// first port (used for single-input nodes). Returns null when no match is found.
        /// </summary>
        private static UnityEditor.Experimental.GraphView.Port FindPort(VisualElement container, string portName)
        {
            UnityEditor.Experimental.GraphView.Port first = null;
            foreach (var child in container.Children())
            {
                if (child is UnityEditor.Experimental.GraphView.Port port)
                {
                    if (first == null) first = port;
                    if (port.portName == portName) return port;
                }
            }
            return string.IsNullOrEmpty(portName) ? first : null;
        }

        /// <summary>
        /// Returns all ports that can receive a connection from <paramref name="startPort"/>.
        /// Allows connections between ports of opposite directions on different nodes.
        /// Override to add domain-specific type constraints.
        /// </summary>
        public override List<UnityEditor.Experimental.GraphView.Port> GetCompatiblePorts(
            UnityEditor.Experimental.GraphView.Port startPort,
            UnityEditor.Experimental.GraphView.NodeAdapter nodeAdapter)
        {
            var result = new List<UnityEditor.Experimental.GraphView.Port>();
            foreach (var port in ports.ToList())
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    result.Add(port);
            }
            return result;
        }

        /// <summary>
        /// Re-applies every edge's colour. Called once the canvas is built (so a source→target gradient can read
        /// its now-connected endpoint nodes' colours) and whenever the <see cref="BaseEdgeView.ColorByEndpoints"/>
        /// toolbar toggle flips.
        /// </summary>
        public void RefreshAllEdgeColors()
        {
            foreach (var el in edges.ToList())
                if (el is BaseEdgeView bev) bev.RefreshColor();
        }

        private void RerouteEdgesWhenMoved(BaseNodeView view)
            => view.RegisterCallback<GeometryChangedEvent>(_ => ScheduleReroute());

        private void ScheduleReroute()
        {
            if (_rerouteScheduled) return;
            _rerouteScheduled = true;
            schedule.Execute(() =>
            {
                _rerouteScheduled = false;
                foreach (var el in edges.ToList())
                    if (el is BaseEdgeView bev) bev.Reroute();
            });
        }
    }
}
