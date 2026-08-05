using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// The non-generic construction core of the fluent graph builder: it accumulates nodes and edges over
    /// graphcore's universal types (Start/Statement/Choice/SubGraph/End), with auto-GUID ids and auto-column
    /// positions. <see cref="GraphBuilder{TGraph}"/> adds the typed <c>Build()</c>. Encodes only universal
    /// construction — zero domain vocabulary; graphcore is untouched.
    /// </summary>
    public abstract class GraphBuilderBase
    {
        private protected readonly List<BaseNodeData> Nodes = new List<BaseNodeData>();
        private protected readonly List<BaseEdgeData> Edges = new List<BaseEdgeData>();
        private protected GraphNodeBuilder Entry;

        private readonly HashSet<GraphNodeBuilder> _handles = new HashSet<GraphNodeBuilder>();
        private int _column;

        /// <summary>Adds a Start node.</summary>
        public GraphNodeBuilder AddStart(string title = null)
            => Add(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, title);

        /// <summary>Adds a Statement node (the workhorse: actions, await-signal, timed wait).</summary>
        public GraphNodeBuilder AddStatement(string title = null)
            => Add(new StatementNodeData { NodeType = StatementNodeData.NodeTypeId }, title);

        /// <summary>Adds a Choice node (add choices with <see cref="GraphNodeBuilder.Choice"/>).</summary>
        public GraphNodeBuilder AddChoice(string title = null)
            => Add(new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId }, title);

        /// <summary>Adds a SubGraph node delegating to <paramref name="target"/>.</summary>
        public GraphNodeBuilder AddSubGraph(string title = null, BaseGraph target = null)
            => Add(new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target }, title);

        /// <summary>Adds an End node carrying <paramref name="reason"/>.</summary>
        public GraphNodeBuilder AddEnd(string title = null, EndReason reason = EndReason.Completed)
            => Add(new EndNodeData { NodeType = EndNodeData.NodeTypeId, EndReason = reason }, title);

#if UNITY_EDITOR
        /// <summary>
        /// Adds a <see cref="GraphLinkNodeData"/> — a NON-executing documentary reference to
        /// <paramref name="target"/> with an optional <paramref name="note"/>. Pure authoring metadata (never
        /// run), so it normally stays unconnected; use it to make composition visible (e.g. "this flow relates
        /// to these quests"). Replaces the raw <c>graph.AddNode(new GraphLinkNodeData{…})</c> boilerplate.
        /// Editor-only: <see cref="GraphLinkNodeData.TargetGraph"/> itself is <c>#if UNITY_EDITOR</c> (it is
        /// GUID-backed and never dereferenced at runtime — see its own class remarks), so there is nothing a
        /// Player-context caller could legitimately do with this node's target anyway.
        /// </summary>
        public GraphNodeBuilder AddGraphLink(BaseGraph target = null, string note = null)
            => Add(new GraphLinkNodeData
            {
                NodeType = GraphLinkNodeData.NodeTypeId,
                TargetGraph = target,
                Note = note
            }, null);   // GraphLink displays its Note, not a Title
#endif

        /// <summary>
        /// Connects <paramref name="from"/> to <paramref name="to"/>. For a Choice <paramref name="from"/>, a
        /// <paramref name="portName"/> matching a choice's <see cref="BaseChoice.Title"/> is resolved to that
        /// choice's id (the routing key); otherwise the port is used literally (default "out").
        /// </summary>
        public GraphBuilderBase Edge(GraphNodeBuilder from, GraphNodeBuilder to, string portName = "out")
            => Edge(from, to, portName, null);

        /// <summary>
        /// As <see cref="Edge(GraphNodeBuilder, GraphNodeBuilder, string)"/>, with an optional
        /// <paramref name="condition"/> gating the edge (the runner skips it when the condition fails).
        /// </summary>
        public GraphBuilderBase Edge(GraphNodeBuilder from, GraphNodeBuilder to, string portName, BaseCondition condition)
        {
            if (from == null || to == null)
                throw new ArgumentNullException(from == null ? nameof(from) : nameof(to));
            if (!_handles.Contains(from) || !_handles.Contains(to))
                throw new ArgumentException("[GraphStandard] GraphBuilder.Edge: a node handle does not belong to this builder.");

            Edges.Add(new BaseEdgeData
            {
                Id         = Guid.NewGuid().ToString("D"),
                FromNodeId = from.Node.Id,
                ToNodeId   = to.Node.Id,
                PortName   = ResolvePort(from.Node, portName),
                Condition  = condition
            });
            return this;
        }

        internal void SetEntry(GraphNodeBuilder node) => Entry = node;

        private GraphNodeBuilder Add(BaseNodeData node, string title)
        {
            node.Id = Guid.NewGuid().ToString("D");
            if (!string.IsNullOrEmpty(title)) node.Title = title;
            node.Position = new Vector2(_column++ * 240f, 0f);
            Nodes.Add(node);
            var handle = new GraphNodeBuilder(this, node);
            _handles.Add(handle);
            return handle;
        }

        private static string ResolvePort(BaseNodeData from, string portName)
        {
            if (from is ChoiceNodeData choice && !string.IsNullOrEmpty(portName))
            {
                var match = choice.Choices.FirstOrDefault(ch => ch != null && ch.Title == portName);
                if (match != null) return match.Id;
                Debug.LogWarning($"[GraphStandard] GraphBuilder.Edge: no choice titled '{portName}' on the choice node; using it literally.");
            }
            return portName;
        }
    }
}
