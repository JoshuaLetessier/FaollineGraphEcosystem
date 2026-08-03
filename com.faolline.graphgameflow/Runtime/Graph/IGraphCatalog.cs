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
    /// </summary>
    public interface IGraphCatalog
    {
        /// <summary>
        /// Resolves <paramref name="graphId"/> to a <see cref="BaseGraph"/>. Exactly one of
        /// <paramref name="onResolved"/>/<paramref name="onFailed"/> is invoked, exactly once, per call.
        /// </summary>
        void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
    }
}
