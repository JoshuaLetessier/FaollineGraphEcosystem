using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphStandard;
using Faolline.GraphStandard.Editor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Builds a graphgameflow asset from a <see cref="PivotQuest"/>'s steps/branches: one node per
    /// step position, linear positions chained directly, shared-position (branch) groups become a
    /// Choice node gated on each step's declared outcome, and every step after a branch reconverges
    /// from every branch member — reconstructing the same topology <see cref="DeclaredColumnBranchStrategy"/>
    /// detected. Each step references its content via SubGraphNodeData (constitution principle VII)
    /// rather than inlining it (FR-008).
    /// </summary>
    public sealed class FlowAssetGenerator : IAssetGenerator
    {
        readonly IProjectAssetResolver _resolver;
        readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _contentFieldsById;

        /// <param name="resolver">
        /// Resolves a step's content reference to the already-existing graph asset it should link to
        /// (e.g. a puzzle or dialogue graph living elsewhere in the project) — the same shared seam
        /// <see cref="DialogueAssetGenerator"/> uses for its own asset lookups. A null result is a
        /// documented-valid "incomplete SubGraph node" state (graphcore skips it with a runtime
        /// warning), never a crash.
        /// </param>
        /// <param name="contentFieldsById">
        /// The same content-table join <see cref="PivotBuilder.BuildContentFields"/> produces for
        /// dialogue path tokens and Speaker folder routing — reused here so a SubGraph node's title
        /// is the content's own declared "name" field (e.g. "Intro joueur de dé") instead of the raw
        /// step id ("S_001") whenever the content table declares one. Falls back to the step id when
        /// null, when the content isn't in the lookup, or when its table declares no "name" field —
        /// never a crash, just a less legible title.
        /// </param>
        public FlowAssetGenerator(IProjectAssetResolver resolver = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> contentFieldsById = null)
        {
            _resolver = resolver ?? new NullProjectAssetResolver();
            _contentFieldsById = contentFieldsById;
        }

        public void Generate(PlanEntry entry)
        {
            var quest = (PivotQuest)entry.Data;
            var builder = new GraphBuilder<GameFlowGraph>();
            var start = builder.AddStart($"{quest.Id} start").AsEntry();

            var exits = new List<GraphNodeBuilder> { start };

            foreach (var group in quest.Steps.GroupBy(s => s.Order).OrderBy(g => g.Key))
            {
                var members = group.ToList();

                if (members.Count == 1)
                {
                    var step = members[0];
                    var node = builder.AddSubGraph(ResolveTitle(step), ResolveContent(step));
                    foreach (var exit in exits)
                        exit.To(node);
                    exits = new List<GraphNodeBuilder> { node };
                }
                else
                {
                    var choice = builder.AddChoice($"{quest.Id} @ {group.Key}");
                    foreach (var exit in exits)
                        exit.To(choice);

                    var branchExits = new List<GraphNodeBuilder>();
                    foreach (var step in members)
                    {
                        choice.Choice(step.BranchOutcome);
                        var node = builder.AddSubGraph(ResolveTitle(step), ResolveContent(step));
                        choice.To(node, step.BranchOutcome);
                        branchExits.Add(node);
                    }
                    exits = branchExits;
                }
            }

            var end = builder.AddEnd($"{quest.Id} done");
            foreach (var exit in exits)
                exit.To(end);

            var graph = builder.Build();
            GraphAssetBuilder.Save(graph, entry.ProposedPath);
        }

        BaseGraph ResolveContent(PivotStep step) =>
            step.ContentRef != null ? _resolver.ResolveGraph(step.ContentRef.TargetTable, step.ContentRef.TargetId) : null;

        string ResolveTitle(PivotStep step)
        {
            if (step.ContentRef != null
                && _contentFieldsById != null
                && _contentFieldsById.TryGetValue(step.ContentRef.TargetId, out var fields)
                && fields.TryGetValue("name", out var name)
                && !string.IsNullOrEmpty(name))
                return name;

            return step.Id;
        }
    }
}
