using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Walks a <see cref="BaseGraph"/>'s action/condition sites and collects every <see cref="ParameterReference"/>
    /// declared through <see cref="IParameterReferencing"/>. This is the declaration-free replacement for the old
    /// per-graph parameter list: <see cref="BaseContext.InitFromGraph"/> uses <see cref="Collect"/> to know which
    /// parameters to seed, and the graph validator uses <see cref="CollectReferences"/> to type-check each site.
    /// <para>
    /// Sites scanned, per node: entry conditions, resume conditions, on-enter actions, on-exit actions; plus each
    /// <see cref="ChoiceNodeData"/> choice's condition; plus each edge's condition. A parameter used only from
    /// host code (never wired into an action/condition) is deliberately not discovered here — the host seeds it
    /// itself via a <c>GraphParams</c> constant.
    /// </para>
    /// </summary>
    public static class GraphParameterScanner
    {
        /// <summary>
        /// Yields every distinct, non-null <see cref="ParameterName"/> referenced by <paramref name="graph"/>'s
        /// actions and conditions, in first-seen order. Empty (never null) for a null graph or one with no
        /// parameter references. Used for seeding.
        /// </summary>
        public static IEnumerable<ParameterName> Collect(BaseGraph graph)
        {
            var seen = new HashSet<ParameterName>();
            foreach (var reference in CollectReferences(graph))
                if (reference.Parameter != null && seen.Add(reference.Parameter))
                    yield return reference.Parameter;
        }

        /// <summary>
        /// Yields every <see cref="ParameterReference"/> (parameter + expected type) from <paramref name="graph"/>'s
        /// action/condition sites, in traversal order and WITHOUT de-duplication — so the validator sees each site
        /// (two sites referencing one parameter with conflicting expected types is exactly the bug to catch).
        /// References with a null parameter are skipped. Empty (never null) for a null graph.
        /// </summary>
        public static IEnumerable<ParameterReference> CollectReferences(BaseGraph graph)
        {
            if (graph == null) yield break;

            if (graph.Nodes != null)
            {
                foreach (var node in graph.Nodes)
                {
                    if (node == null) continue;
                    foreach (var r in FromConditions(node.EntryConditions))  yield return r;
                    foreach (var r in FromConditions(node.ResumeConditions)) yield return r;
                    foreach (var r in FromActions(node.OnEnterActions))      yield return r;
                    foreach (var r in FromActions(node.OnExitActions))       yield return r;

                    if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null)
                        foreach (var choice in choiceNode.Choices)
                            if (choice?.Condition is IParameterReferencing cr)
                                foreach (var r in FromReferencer(cr)) yield return r;
                }
            }

            if (graph.Edges != null)
            {
                foreach (var edge in graph.Edges)
                    if (edge?.Condition is IParameterReferencing er)
                        foreach (var r in FromReferencer(er)) yield return r;
            }
        }

        private static IEnumerable<ParameterReference> FromActions(IEnumerable<BaseAction> actions)
        {
            if (actions == null) yield break;
            foreach (var action in actions)
                if (action is IParameterReferencing r)
                    foreach (var reference in FromReferencer(r))
                        yield return reference;
        }

        private static IEnumerable<ParameterReference> FromConditions(IEnumerable<BaseCondition> conditions)
        {
            if (conditions == null) yield break;
            foreach (var condition in conditions)
                if (condition is IParameterReferencing r)
                    foreach (var reference in FromReferencer(r))
                        yield return reference;
        }

        private static IEnumerable<ParameterReference> FromReferencer(IParameterReferencing referencer)
        {
            if (referencer?.ReferencedParameters == null) yield break;
            foreach (var reference in referencer.ReferencedParameters)
                if (reference.Parameter != null)
                    yield return reference;
        }
    }
}
