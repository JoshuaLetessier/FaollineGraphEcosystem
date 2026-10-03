using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Checks an execution path (root first, see <see cref="GraphRunFrame"/>) against the graphs it walks, without
    /// modifying anything. Graph 0 is the supplied root; every deeper graph is reached ONLY as the target of the
    /// enclosing level's <see cref="SubGraphNodeData"/>, so no graph is ever looked up by id.
    /// </summary>
    public static class GraphRunPath
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="path"/> can be rebuilt from <paramref name="rootGraph"/>;
        /// otherwise <c>false</c> with the first mismatch in <paramref name="error"/>. Pure: modifies nothing, logs nothing.
        /// </summary>
        public static bool TryResolve(BaseGraph rootGraph, IReadOnlyList<GraphRunFrame> path, out GraphRunPathError error)
            => TryResolve(rootGraph, path, out _, out error);

        /// <summary>
        /// As <see cref="TryResolve(BaseGraph, IReadOnlyList{GraphRunFrame}, out GraphRunPathError)"/>, also
        /// returning the graph of every level (root first) on success.
        /// </summary>
        public static bool TryResolve(BaseGraph rootGraph, IReadOnlyList<GraphRunFrame> path,
                                      out IReadOnlyList<BaseGraph> graphs, out GraphRunPathError error)
        {
            graphs = null;
            error  = null;

            if (rootGraph == null)
                return Fail(out error, GraphRunPathErrorReason.NullRootGraph, 0, -1, "No root graph was supplied.");
            if (path == null || path.Count == 0)
                return Fail(out error, GraphRunPathErrorReason.EmptyPath, -1, -1, "The execution path is empty.");

            var resolved = new List<BaseGraph>(path.Count);
            var graph    = rootGraph;
            for (int i = 0; i < path.Count; i++)
            {
                var frame = path[i];
                if (frame == null)
                    return Fail(out error, GraphRunPathErrorReason.NodeMissing, i, i - 1, $"Frame {i} is missing.");

                if (i == 0 && frame.GraphId != rootGraph.GraphId)
                    return Fail(out error, GraphRunPathErrorReason.RootGraphMismatch, 0, -1,
                        $"Frame 0: the saved root graph '{frame.GraphId}' is not the supplied root graph {Describe(rootGraph)}.");

                for (int j = 0; j < i; j++)
                {
                    if (path[j].GraphId == frame.GraphId)
                        return Fail(out error, GraphRunPathErrorReason.RepeatedGraph, i, i - 1,
                            $"Frame {i}: graph '{frame.GraphId}' already appears at frame {j}; a run never enters the same graph twice.");
                }

                var node = FindNode(graph, frame.NodeId);
                if (node == null)
                    return Fail(out error, GraphRunPathErrorReason.NodeMissing, i, i - 1,
                        $"Frame {i}: node '{frame.NodeId}' no longer exists in graph {Describe(graph)}.");

                resolved.Add(graph);
                if (i == path.Count - 1) break;

                // An enclosing level: its node must still enter the next level's graph, the same way.
                if (!(node is SubGraphNodeData sub))
                    return Fail(out error, GraphRunPathErrorReason.NotASubGraphNode, i, i,
                        $"Frame {i}: node '{node.Id}' in graph {Describe(graph)} is no longer a sub-graph node, so frame {i + 1} cannot be reached.");
                if (sub.TargetGraph == null)
                    return Fail(out error, GraphRunPathErrorReason.SubGraphTargetMissing, i, i,
                        $"Frame {i}: sub-graph node '{node.Id}' in graph {Describe(graph)} has no target graph.");

                var next = path[i + 1];
                if (next != null)   // a null next frame is reported as missing on the next iteration
                {
                    if (sub.TargetGraph.GraphId != next.GraphId)
                        return Fail(out error, GraphRunPathErrorReason.SubGraphTargetMismatch, i, i,
                            $"Frame {i}: sub-graph node '{node.Id}' now targets graph {Describe(sub.TargetGraph)} instead of the saved graph '{next.GraphId}'.");
                    var mode = ContextModeOf(sub);
                    if (mode != next.ContextMode)
                        return Fail(out error, GraphRunPathErrorReason.ContextModeMismatch, i, i,
                            $"Frame {i}: sub-graph node '{node.Id}' now enters its graph as {mode} instead of the saved {next.ContextMode}.");
                }

                graph = sub.TargetGraph;
            }

            graphs = resolved;
            return true;
        }

        /// <summary>The mode <paramref name="node"/> enters its graph with — the same precedence as the runner.</summary>
        internal static GraphRunContextMode ContextModeOf(SubGraphNodeData node)
        {
            if (node.OpensScope) return GraphRunContextMode.Scoped;
            return node.InheritParentContext ? GraphRunContextMode.Shared : GraphRunContextMode.Fresh;
        }

        private static BaseNodeData FindNode(BaseGraph graph, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            foreach (var node in graph.Nodes)
                if (node != null && node.Id == nodeId) return node;
            return null;
        }

        private static string Describe(BaseGraph graph)
            => string.IsNullOrEmpty(graph.name) ? $"'{graph.GraphId}'" : $"'{graph.name}' ('{graph.GraphId}')";

        private static bool Fail(out GraphRunPathError error, GraphRunPathErrorReason reason, int frameIndex,
                                 int deepestValidFrameIndex, string message)
        {
            error = new GraphRunPathError(reason, frameIndex, deepestValidFrameIndex, message);
            return false;
        }
    }
}
