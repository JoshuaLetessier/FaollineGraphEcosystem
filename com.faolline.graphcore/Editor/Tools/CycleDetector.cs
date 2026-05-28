using System.Collections.Generic;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Stateless utility that detects cycles in the BaseGraph asset dependency graph
    /// via iterative DFS over SubGraphNodeData.TargetGraph references.
    /// </summary>
    public static class CycleDetector
    {
        /// <summary>
        /// Checks whether adding a reference from <paramref name="root"/> to
        /// <paramref name="proposed"/> would create a cycle in the dependency graph.
        /// </summary>
        /// <param name="root">The graph that would contain the new SubGraph reference.</param>
        /// <param name="proposed">The graph that would be referenced by the new SubGraph node.</param>
        public static CycleDetectionResult Check(BaseGraph root, BaseGraph proposed)
        {
            if (root == null || proposed == null)
                return new CycleDetectionResult(false, null);

            // Self-cycle
            if (proposed.GraphId == root.GraphId)
                return new CycleDetectionResult(true, new List<string> { root.GraphId });

            // Iterative DFS: starting from proposed, check if we can reach root
            var visited = new HashSet<string>();
            var stack = new Stack<(BaseGraph graph, List<string> path)>();
            stack.Push((proposed, new List<string> { proposed.GraphId }));

            while (stack.Count > 0)
            {
                var (current, path) = stack.Pop();

                if (!visited.Add(current.GraphId))
                    continue;

                foreach (var node in current.Nodes)
                {
                    if (node is SubGraphNodeData subGraph && subGraph.TargetGraph != null)
                    {
                        var targetId = subGraph.TargetGraph.GraphId;

                        if (targetId == root.GraphId)
                        {
                            var cyclePath = new List<string>(path) { root.GraphId };
                            return new CycleDetectionResult(true, cyclePath);
                        }

                        if (!visited.Contains(targetId))
                        {
                            var newPath = new List<string>(path) { targetId };
                            stack.Push((subGraph.TargetGraph, newPath));
                        }
                    }
                }
            }

            return new CycleDetectionResult(false, null);
        }
    }
}
