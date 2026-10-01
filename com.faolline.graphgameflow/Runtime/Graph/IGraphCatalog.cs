using System;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// The single seam through which a host resolves a stable <see cref="BaseGraph.GraphId"/> to its concrete
    /// asset, independent of any specific loading technology — the graph-side mirror of <see cref="ISceneLoader"/>.
    /// Needed the moment a project has more than one independently-loadable root graph: <c>GraphRunSnapshot.GraphId</c>
    /// is informational only, and its <c>Restore</c> requires the caller to already have the <see cref="BaseGraph"/>
    /// in hand, so restoring a save needs a <c>GraphId → asset</c> resolution step from somewhere.
    /// <para>
    /// Callback-based (not <c>Task</c>/<c>async</c>) to match <see cref="ISceneLoader"/>'s own synchronous-call/
    /// async-callback idiom — resolution may be instantaneous (a direct in-memory lookup) or take several frames
    /// (an Addressables load); the caller never has to branch on which.
    /// </para>
    /// <para>
    /// A resolve may load content that stays in memory until <see cref="Release"/> is called for that key, so
    /// the same holds for releasing: the caller releases through this interface whatever the implementation.
    /// </para>
    /// </summary>
    public interface IGraphCatalog
    {
        /// <summary>
        /// Resolves <paramref name="graphId"/> to a <see cref="BaseGraph"/>. Exactly one of
        /// <paramref name="onResolved"/>/<paramref name="onFailed"/> is invoked, exactly once, per call.
        /// </summary>
        void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);

        /// <summary>
        /// Releases everything this catalog holds for <paramref name="graphId"/>: every successful
        /// <see cref="Resolve"/> made with that same key, not just the latest one. Call it once the resolved
        /// graph is no longer used. No-op when nothing is held for that key (or for catalogs that load nothing).
        /// <para>
        /// Scoped to the key, not to the caller: if two callers resolved the same key through the same catalog
        /// (e.g. the one shared through <see cref="GameFlowContext.GraphCatalog"/>), one release frees the graph
        /// for both, so the owner of a key's lifetime must be a single place. Releasing never forgets how to
        /// resolve the key: a later <see cref="Resolve"/> loads it again.
        /// </para>
        /// </summary>
        void Release(string graphId);
    }
}
