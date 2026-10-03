namespace Faolline.GraphCore
{
    /// <summary>
    /// Immutable description of one level of a run's execution stack: the graph it runs, the node it is on, and
    /// how it holds values. A level that is not the deepest always sits on the <see cref="SubGraphNodeData"/>
    /// that entered the next level. Read a live run's levels with <see cref="BaseRunner.GetExecutionPath"/>;
    /// rebuild a run from them with <see cref="BaseRunner.StartFromPath"/>.
    /// </summary>
    public sealed class GraphRunFrame
    {
        /// <summary>Describes one level of an execution path.</summary>
        /// <param name="graphId">The <see cref="BaseGraph.GraphId"/> this level runs.</param>
        /// <param name="nodeId">The id of the node this level is on.</param>
        /// <param name="contextMode">How this level holds values relative to the level above it.</param>
        public GraphRunFrame(string graphId, string nodeId, GraphRunContextMode contextMode = GraphRunContextMode.Shared)
        {
            GraphId     = graphId;
            NodeId      = nodeId;
            ContextMode = contextMode;
        }

        /// <summary>The <see cref="BaseGraph.GraphId"/> this level runs.</summary>
        public string GraphId { get; }

        /// <summary>The id of the node this level is on.</summary>
        public string NodeId { get; }

        /// <summary>
        /// How this level holds values relative to the level above it. Ignored for the root level (index 0).
        /// </summary>
        public GraphRunContextMode ContextMode { get; }

        /// <inheritdoc/>
        public override string ToString() => $"{GraphId}:{NodeId} ({ContextMode})";
    }
}
