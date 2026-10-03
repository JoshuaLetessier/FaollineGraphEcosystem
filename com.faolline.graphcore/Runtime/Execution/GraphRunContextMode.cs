namespace Faolline.GraphCore
{
    /// <summary>
    /// How one level of a run's execution stack holds its values relative to the level that entered it — the
    /// three behaviours a <see cref="SubGraphNodeData"/> offers. Part of a saved execution path
    /// (<see cref="GraphRunFrame"/>), so the integer values are pinned and must never change.
    /// </summary>
    public enum GraphRunContextMode
    {
        /// <summary>
        /// The same context as the enclosing level (<see cref="SubGraphNodeData.InheritParentContext"/>, no scope).
        /// Also the mode reported for the root level, which has no enclosing level.
        /// </summary>
        Shared = 0,

        /// <summary>
        /// The enclosing level's context plus a local overlay seeded from the sub-graph and discarded when it ends
        /// (<see cref="SubGraphNodeData.OpensScope"/>).
        /// </summary>
        Scoped = 1,

        /// <summary>
        /// A separate, fresh context seeded from the sub-graph's declared defaults (neither
        /// <see cref="SubGraphNodeData.OpensScope"/> nor <see cref="SubGraphNodeData.InheritParentContext"/>).
        /// </summary>
        Fresh = 2
    }
}
