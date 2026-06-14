using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// Typed context for standalone quest use (graphcore Typed Context Contract). A gameflow host may instead pass
    /// its own <see cref="BaseContext"/> to <see cref="QuestEvaluator"/>. Overriding
    /// <see cref="CreateCloneInstance"/> is mandatory so GoBack/history snapshot restoration preserves the subtype.
    /// </summary>
    public sealed class QuestContext : BaseContext
    {
        /// <summary>True if <paramref name="objectiveId"/> is recorded completed for quest <paramref name="questId"/>.</summary>
        public bool IsObjectiveCompleted(string questId, string objectiveId)
            => CollectionContains(QuestContextKeys.CompletedSet(questId), objectiveId);

        /// <summary>True if <paramref name="objectiveId"/> is recorded failed for quest <paramref name="questId"/>.</summary>
        public bool IsObjectiveFailed(string questId, string objectiveId)
            => CollectionContains(QuestContextKeys.FailedSet(questId), objectiveId);

        /// <inheritdoc/>
        protected override BaseContext CreateCloneInstance() => new QuestContext();
    }
}
