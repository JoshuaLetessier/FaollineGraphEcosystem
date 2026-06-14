using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// Sub-builder for one objective, returned by <see cref="QuestBuilder.AddObjective"/>. Chains back to the
    /// owning <see cref="QuestBuilder"/> via <see cref="AddObjective"/>, <see cref="RewardQuestWith"/>,
    /// <see cref="UnlockWhen"/>, and <see cref="Build"/>.
    /// </summary>
    public sealed class ObjectiveBuilder
    {
        private readonly QuestBuilder _quest;
        private readonly QuestBuilder.ObjectiveSpec _spec;

        internal ObjectiveBuilder(QuestBuilder quest, QuestBuilder.ObjectiveSpec spec)
        {
            _quest = quest;
            _spec = spec;
        }

        /// <summary>Sets the completion condition (when it holds, the objective is recorded Completed).</summary>
        public ObjectiveBuilder CompleteWhen(BaseCondition condition) { _spec.Completion = condition; return this; }

        /// <summary>Sets the optional fail condition (checked before completion — fail precedes complete).</summary>
        public ObjectiveBuilder FailWhen(BaseCondition condition) { _spec.Fail = condition; return this; }

        /// <summary>Marks this objective optional (it tracks state + rewards but does not block quest completion).</summary>
        public ObjectiveBuilder Optional() { _spec.Required = false; return this; }

        /// <summary>Sets the reward fired once when this objective completes.</summary>
        public ObjectiveBuilder RewardWith(BaseAction reward) { _spec.Reward = reward; return this; }

        /// <summary>Declares prerequisites: one id is a chain link; several form a DAG join (all must complete).</summary>
        public ObjectiveBuilder Requires(params string[] prerequisiteObjectiveIds)
        {
            if (prerequisiteObjectiveIds != null)
                foreach (var id in prerequisiteObjectiveIds)
                    if (!string.IsNullOrEmpty(id))
                        _spec.Requires.Add(id);
            return this;
        }

        /// <summary>Declares the next objective on the owning quest.</summary>
        public ObjectiveBuilder AddObjective(string objectiveId) => _quest.AddObjective(objectiveId);

        /// <summary>Sets the quest-level unlock condition on the owning quest.</summary>
        public ObjectiveBuilder UnlockWhen(BaseCondition condition) { _quest.UnlockWhen(condition); return this; }

        /// <summary>Sets the quest completion reward on the owning quest.</summary>
        public ObjectiveBuilder RewardQuestWith(BaseAction reward) { _quest.RewardQuestWith(reward); return this; }

        /// <summary>Validates and builds the owning <see cref="QuestGraph"/>.</summary>
        public QuestGraph Build() => _quest.Build();
    }
}
