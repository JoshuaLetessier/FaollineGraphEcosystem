using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// One quest: a graphcore <see cref="BaseGraph"/> whose nodes are <see cref="ObjectiveNodeData"/> and whose
    /// edges are the prerequisite DAG (<c>From→To</c> = "To requires From"), consumed by graphstandard's
    /// <c>ReactiveEvaluator</c>. Carries quest-level metadata: a logical id, an optional unlock condition, an
    /// optional completion reward, and a completion rule.
    /// </summary>
    public sealed class QuestGraph : BaseGraph
    {
        [SerializeField] private string _questId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea] private string _description = string.Empty;
        [SerializeField] private BaseCondition _unlockCondition;
        [SerializeField] private BaseAction _completionReward;
        [SerializeField] private QuestCompletionRule _completionRule = QuestCompletionRule.AllRequired;

        /// <summary>Stable logical id (scopes the quest's context collections). Set by <see cref="QuestBuilder"/>.</summary>
        public string QuestId { get => _questId; set => _questId = value ?? string.Empty; }

        /// <summary>Display title for a quest journal UI. Falls back to <see cref="QuestId"/> when empty. Never null.</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _questId : _displayName;
            set => _displayName = value ?? string.Empty;
        }

        /// <summary>Optional longer description for a quest journal UI. Never null.</summary>
        public string Description { get => _description; set => _description = value ?? string.Empty; }

        /// <summary>Optional. While non-null and unmet, the quest is Locked and surfaces no Active objectives.</summary>
        public BaseCondition UnlockCondition { get => _unlockCondition; set => _unlockCondition = value; }

        /// <summary>Optional. Executed once when the quest enters Completed.</summary>
        public BaseAction CompletionReward { get => _completionReward; set => _completionReward = value; }

        /// <summary>How quest completion is decided from its objectives. Default <see cref="QuestCompletionRule.AllRequired"/>.</summary>
        public QuestCompletionRule CompletionRule { get => _completionRule; set => _completionRule = value; }
    }
}
