namespace Faolline.GraphQuest
{
    /// <summary>
    /// The derived progress state of a quest OR an objective. Recomputed from the shared
    /// <see cref="Faolline.GraphCore.BaseContext"/> on each evaluation pass — never stored as the source of truth.
    /// </summary>
    public enum QuestState
    {
        /// <summary>Prerequisites are unmet (or the owning quest's unlock condition is unmet).</summary>
        Locked = 0,

        /// <summary>Unlocked and in progress (maps to <see cref="Faolline.GraphStandard.ReactiveNodeState.Available"/>).</summary>
        Active = 1,

        /// <summary>The completion condition has held; the id is recorded in the completed-set.</summary>
        Completed = 2,

        /// <summary>The fail condition has held; the id is recorded in the failed-set (fail precedes complete).</summary>
        Failed = 3
    }
}
