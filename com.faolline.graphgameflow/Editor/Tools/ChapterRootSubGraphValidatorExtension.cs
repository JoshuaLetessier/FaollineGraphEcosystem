using UnityEditor;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Gives graphcore's generic <see cref="IGraphValidatorExtension"/> seam its actual "chapter root" meaning:
    /// warns when a <c>SubGraphNodeData</c>'s hard reference targets a graph registered (via
    /// <see cref="GraphKeySourceRegistry"/>) as an independently-loadable entry point. That hard reference would
    /// silently reintroduce the full build-time pull the soft-reference lots of this feature exist to remove —
    /// the intended crossing mechanisms are a documentary <c>GraphLinkNodeData</c> or an Addressables preload
    /// action, never <c>SubGraphNodeData</c>. graphcore itself never learns what "chapter root" means; this
    /// class is the one place that knowledge lives.
    /// </summary>
    public sealed class ChapterRootSubGraphValidatorExtension : IGraphValidatorExtension
    {
        /// <inheritdoc/>
        public string CheckSubGraphTarget(BaseGraph targetGraph)
        {
            if (targetGraph == null) return null;

            var path = AssetDatabase.GetAssetPath(targetGraph);
            if (string.IsNullOrEmpty(path)) return null;   // not a project asset (e.g. an in-memory test fixture) — nothing to promote
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return null;

            foreach (var provider in GraphKeySourceRegistry.Providers)
            {
                if (provider != null && provider.TryResolveGuid(guid, out var key))
                {
                    return $"Sub-graph invokes '{targetGraph.name}', which is registered as a chapter-root graph " +
                           $"under key '{key}' — this hard reference will pull the target's full dependency " +
                           "closure into the build, silently defeating its soft-loading setup. Use a documentary " +
                           "GraphLink or the Addressables preload action instead.";
                }
            }

            return null;
        }

        [InitializeOnLoadMethod]
        private static void Register() => GraphValidatorExtensionRegistry.Register(new ChapterRootSubGraphValidatorExtension());
    }
}
