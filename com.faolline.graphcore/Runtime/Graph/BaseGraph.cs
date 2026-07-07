using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Root container asset for a graph. Owns all nodes, edges, and parameters.
    /// <see cref="GraphId"/> is a stable GUID assigned once in <c>OnEnable</c> and never changed.
    /// </summary>
    // No [CreateAssetMenu] — consumers create typed graphs (DialogueGraph, GameFlowGraph, etc.), not raw BaseGraph.
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphcore/README.md")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_graph_base.png")]
    public class BaseGraph : ScriptableObject, IStableGuidIdentity
    {
        [SerializeField, HideInInspector] private string             _graphId;
        [SerializeReference] private List<BaseNodeData> _nodes      = new List<BaseNodeData>();
        [SerializeReference] private List<BaseEdgeData> _edges      = new List<BaseEdgeData>();
        [SerializeField, Tooltip("Typed parameters declared on this graph. InitFromGraph seeds the context with these defaults.")]
        private List<ParameterData>  _parameters = new List<ParameterData>();
        [SerializeField]   private List<GraphGroupData> _groups     = new List<GraphGroupData>();
        [SerializeField, HideInInspector] private string             _entryNodeId;
        [SerializeField, Tooltip("Maximum history entries kept by the runtime for GoBack / GoBackToCheckpoint. Default 20. Set to 0 for unlimited.")]
        private int                _historyDepth = 20;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_graphId))
            {
                _graphId = Guid.NewGuid().ToString("D");
#if UNITY_EDITOR
                StableGuidPersistence.ScheduleSave(this);   // persist the assignment — see StableGuidPersistence
#endif
            }
        }

        /// <summary>
        /// Stable GUID that uniquely identifies this graph asset. Assigned once in
        /// <c>OnEnable</c> and never overwritten. Read-only.
        /// </summary>
        public string GraphId => _graphId;

        // Explicit implementation: kept out of BaseGraph's normal public surface (GraphId already exposes
        // this under its own name). Lets the editor's stable-id duplicate detector discover this type via
        // IStableGuidIdentity with no per-type code in the detector.
        string IStableGuidIdentity.StableId => _graphId;
        string IStableGuidIdentity.StableIdFieldName => nameof(_graphId);

        /// <summary>All nodes in this graph.</summary>
        public IReadOnlyList<BaseNodeData> Nodes => _nodes;

        /// <summary>All directed edges in this graph.</summary>
        public IReadOnlyList<BaseEdgeData> Edges => _edges;

        /// <summary>All typed parameters declared on this graph.</summary>
        public IReadOnlyList<ParameterData> Parameters => _parameters;

        /// <summary>Id of the node that serves as the graph entry point.</summary>
        public string EntryNodeId
        {
            get => _entryNodeId;
            set => _entryNodeId = value;
        }

        /// <summary>
        /// Maximum history entries kept by the runtime. Default: 20. Set to 0 for unlimited.
        /// <para>
        /// <b>Memory cost of 0 (unlimited):</b> each <see cref="BaseRunner.Proceed"/> call deep-clones
        /// the entire <see cref="BaseContext"/> (all parameters, collections, and local overlays) into a
        /// new history entry. On a long-running graph with a large context this accumulates one full clone
        /// per step with no cap — memory grows linearly with traversal length. Keep the default (20) unless
        /// full rewind is required, and consider checkpointing instead of unlimited history.
        /// </para>
        /// Enforcement is the runtime layer's responsibility.
        /// </summary>
        public int HistoryDepth
        {
            get => _historyDepth;
            set => _historyDepth = value;
        }

        /// <summary>All node groups on this graph (authoring aid, no runtime effect).</summary>
        public IReadOnlyList<GraphGroupData> Groups => _groups;

        /// <summary>Appends a group to this graph. Use from editor tooling only.</summary>
        public void AddGroup(GraphGroupData group) => _groups.Add(group);

        /// <summary>Removes a group from this graph. Use from editor tooling only.</summary>
        public void RemoveGroup(GraphGroupData group) => _groups.Remove(group);

        /// <summary>Appends a node to this graph. Use from editor tooling only.</summary>
        public void AddNode(BaseNodeData node) => _nodes.Add(node);

        /// <summary>Removes a node from this graph. Use from editor tooling only.</summary>
        public void RemoveNode(BaseNodeData node) => _nodes.Remove(node);

        /// <summary>Appends an edge to this graph. Use from editor tooling only.</summary>
        public void AddEdge(BaseEdgeData edge) => _edges.Add(edge);

        /// <summary>Removes an edge from this graph. Use from editor tooling only.</summary>
        public void RemoveEdge(BaseEdgeData edge) => _edges.Remove(edge);

        /// <summary>Appends a parameter to this graph. Use from editor tooling only.</summary>
        public void AddParameter(ParameterData parameter) => _parameters.Add(parameter);

        /// <summary>Removes a parameter from this graph. Use from editor tooling only.</summary>
        public void RemoveParameter(ParameterData parameter) => _parameters.Remove(parameter);

    }
}
