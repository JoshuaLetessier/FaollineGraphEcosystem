namespace Faolline.GraphCore
{
    /// <summary>Why an execution path does not match the graphs it is checked against (<see cref="GraphRunPath"/>).</summary>
    public enum GraphRunPathErrorReason
    {
        /// <summary>No root graph was supplied.</summary>
        NullRootGraph,

        /// <summary>The path is null or has no level.</summary>
        EmptyPath,

        /// <summary>The path's root level names a graph other than the supplied root graph.</summary>
        RootGraphMismatch,

        /// <summary>
        /// A graph appears twice in the path. A live run never produces this (entering a graph already on the
        /// stack throws <see cref="GraphCycleException"/>), so it means a corrupted or edited save, or cyclic content.
        /// </summary>
        RepeatedGraph,

        /// <summary>A level's node is missing, empty, or no longer exists in that level's graph.</summary>
        NodeMissing,

        /// <summary>An enclosing level's node is no longer a <see cref="SubGraphNodeData"/>, so the next level cannot be reached.</summary>
        NotASubGraphNode,

        /// <summary>An enclosing level's <see cref="SubGraphNodeData"/> has no target graph.</summary>
        SubGraphTargetMissing,

        /// <summary>An enclosing level's <see cref="SubGraphNodeData"/> now targets a graph other than the next level's.</summary>
        SubGraphTargetMismatch,

        /// <summary>An enclosing level's <see cref="SubGraphNodeData"/> now enters its graph with another <see cref="GraphRunContextMode"/>.</summary>
        ContextModeMismatch
    }
}
