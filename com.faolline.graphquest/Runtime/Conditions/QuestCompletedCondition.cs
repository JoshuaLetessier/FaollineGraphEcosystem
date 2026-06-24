using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// A graphcore <see cref="BaseCondition"/> that holds when ALL of the listed quests are currently Completed
    /// (reading the shared <see cref="QuestContextKeys.CompletedQuests"/> set that each <see cref="QuestEvaluator"/>
    /// keeps in sync). Use it to chain quests — e.g. gate a quest's <c>UnlockWhen</c> on a prior quest's completion.
    /// An empty list is vacuously true. Build one with <see cref="For"/> or the builder's <c>UnlockAfter</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphQuest/Conditions/Quest Completed", fileName = "QuestCompletedCondition")]
    public sealed class QuestCompletedCondition : BaseCondition
    {
        [SerializeField, Tooltip("Quest ids that must ALL be Completed for this condition to hold. Empty list = vacuously true.")]
        private List<string> _questIds = new List<string>();

        /// <summary>The quest ids that must all be Completed for this condition to hold.</summary>
        public List<string> QuestIds => _questIds;

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (context == null) return false;
            foreach (var id in _questIds)
                if (!string.IsNullOrEmpty(id) && !context.CollectionContains(QuestContextKeys.CompletedQuests, id))
                    return false;
            return true;
        }

        /// <summary>Creates a condition requiring all of <paramref name="questIds"/> to be Completed.</summary>
        public static QuestCompletedCondition For(params string[] questIds)
        {
            var c = CreateInstance<QuestCompletedCondition>();
            if (questIds != null)
                foreach (var id in questIds)
                    if (!string.IsNullOrEmpty(id))
                        c._questIds.Add(id);
            return c;
        }
    }
}
