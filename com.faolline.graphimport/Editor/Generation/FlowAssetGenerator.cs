using System;
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
        readonly Func<PivotReference, BaseGraph> _contentResolver;

        /// <param name="contentResolver">
        /// Resolves a step's content reference to the already-existing graph asset it should link to
        /// (e.g. a puzzle or dialogue graph living elsewhere in the project). Locating that asset from
        /// a bare table/id pair is not yet part of this feature's scope — the default resolver always
        /// returns null, which graphcore documents as a valid "incomplete SubGraph node" state (skipped
        /// with a runtime warning), never a crash.
        /// </param>
        public FlowAssetGenerator(Func<PivotReference, BaseGraph> contentResolver = null)
        {
            _contentResolver = contentResolver ?? (_ => null);
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
                    var node = builder.AddSubGraph(step.Id, ResolveContent(step));
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
                        var node = builder.AddSubGraph(step.Id, ResolveContent(step));
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

        BaseGraph ResolveContent(PivotStep step) => step.ContentRef != null ? _contentResolver(step.ContentRef) : null;
    }
}
