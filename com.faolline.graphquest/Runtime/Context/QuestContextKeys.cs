namespace Faolline.GraphQuest
{
    /// <summary>
    /// The only place the quest collection-key string literals exist (graphcore Typed Context Contract). State is
    /// held in three <see cref="Faolline.GraphCore.BaseContext"/> string-set collections, scoped per quest so
    /// several quests can share one context without their objective ids colliding.
    /// </summary>
    public static class QuestContextKeys
    {
        /// <summary>Base key for the completed-set (also the <c>ReactiveEvaluator</c> completed-set).</summary>
        public const string Completed = "quest_completed";

        /// <summary>Base key for the failed-set.</summary>
        public const string Failed = "quest_failed";

        /// <summary>Base key for the rewarded-set (one-shot reward guard).</summary>
        public const string Rewarded = "quest_rewarded";

        /// <summary>The rewarded-set marker for a quest's own completion reward (distinct from any objective id).</summary>
        public const string QuestRewardMarker = "__quest__";

        /// <summary>
        /// Shared (NOT per-quest) collection of the ids of quests that are currently Completed. A quest's evaluator
        /// syncs its own id in/out of this set, so another quest can gate its unlock on it (cross-quest chaining).
        /// </summary>
        public const string CompletedQuests = "quest_done";

        /// <summary>The per-quest completed-set collection key.</summary>
        public static string CompletedSet(string questId) => Scoped(Completed, questId);

        /// <summary>The per-quest failed-set collection key.</summary>
        public static string FailedSet(string questId) => Scoped(Failed, questId);

        /// <summary>The per-quest rewarded-set collection key.</summary>
        public static string RewardedSet(string questId) => Scoped(Rewarded, questId);

        /// <summary>Base key for a timed objective's deadline param (absolute game time at which it fails).</summary>
        public const string Deadline = "quest_deadline";

        /// <summary>The per-objective deadline param key (a <c>float</c> absolute game time).</summary>
        public static string DeadlineKey(string questId, string objectiveId)
            => Deadline + ":" + questId + ":" + objectiveId;

        private static string Scoped(string prefix, string questId)
            => string.IsNullOrEmpty(questId) ? prefix : prefix + ":" + questId;
    }
}
