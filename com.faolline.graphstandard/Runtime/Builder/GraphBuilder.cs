using System.Linq;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Fluent, code-first builder for any <see cref="BaseGraph"/> subclass — construct a graph (nodes, edges,
    /// actions, await/wait/checkpoint, choices, entry node) in a few readable lines instead of GUID ids and
    /// <c>AddNode</c>/<c>AddEdge</c> boilerplate. Add nodes with <c>AddStart/AddStatement/AddChoice/
    /// AddSubGraph/AddEnd</c> (each returns a <see cref="GraphNodeBuilder"/> handle), wire them with
    /// <see cref="GraphBuilderBase.Edge"/> or <see cref="GraphNodeBuilder.To"/>, then call <see cref="Build"/>.
    /// </summary>
    public sealed class GraphBuilder<TGraph> : GraphBuilderBase where TGraph : BaseGraph
    {
        /// <summary>
        /// Assembles a fresh <typeparamref name="TGraph"/> from the accumulated nodes and edges and sets its
        /// entry node (the one marked <see cref="GraphNodeBuilder.AsEntry"/>, else the first Start added).
        /// </summary>
        public TGraph Build()
        {
            var graph = ScriptableObject.CreateInstance<TGraph>();
            foreach (var node in Nodes) graph.AddNode(node);
            foreach (var edge in Edges) graph.AddEdge(edge);

            if (Entry != null)
                graph.EntryNodeId = Entry.Node.Id;
            else
            {
                var start = Nodes.FirstOrDefault(n => n is StartNodeData);
                if (start != null) graph.EntryNodeId = start.Id;
            }
            return graph;
        }
    }
}
