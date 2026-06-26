namespace Faolline.GraphQuest
{
    /// <summary>How a quest's completion is decided from its objectives.</summary>
    public enum QuestCompletionRule
    {
        /// <summary>The quest is Completed when every <c>Required</c> objective is Completed.</summary>
        AllRequired = 0,
        /// <summary>The quest is Completed when at least one <c>Required</c> objective is Completed.</summary>
        AnyRequired = 1,
        /// <summary>The quest is Completed when at least <see cref="QuestGraph.CompletionThreshold"/> <c>Required</c> objectives are Completed.</summary>
        Threshold = 2
    }
}
