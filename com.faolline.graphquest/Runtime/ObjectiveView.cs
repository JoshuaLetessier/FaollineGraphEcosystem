namespace Faolline.GraphQuest
{
    /// <summary>
    /// A read-only snapshot of one objective for a quest journal / tracker UI: its id, display label and
    /// description, whether it is required, its current <see cref="QuestState"/>, and optional progress
    /// (e.g. 3/10). The library ships this data; the consumer renders it (in-game UI is consumer territory).
    /// </summary>
    public readonly struct ObjectiveView
    {
        /// <summary>The objective id.</summary>
        public string Id { get; }

        /// <summary>The short display label (the objective's Title, or its id when no title was set).</summary>
        public string DisplayName { get; }

        /// <summary>The longer description (empty when none).</summary>
        public string Description { get; }

        /// <summary>Whether this objective is required for quest completion.</summary>
        public bool Required { get; }

        /// <summary>The objective's current derived state.</summary>
        public QuestState State { get; }

        /// <summary>Current progress count (e.g. 3 out of 10). 0 when no progress tracking is configured.</summary>
        public int Progress { get; }

        /// <summary>Target progress count (e.g. 10). 0 when no progress tracking is configured.</summary>
        public int ProgressTarget { get; }

        /// <summary>True when this objective has progress tracking configured (<see cref="ProgressTarget"/> &gt; 0).</summary>
        public bool HasProgress => ProgressTarget > 0;

        public ObjectiveView(string id, string displayName, string description, bool required, QuestState state,
            int progress = 0, int progressTarget = 0)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Required = required;
            State = state;
            Progress = progress;
            ProgressTarget = progressTarget;
        }
    }
}
