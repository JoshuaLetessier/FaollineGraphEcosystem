namespace Faolline.GraphQuest
{
    /// <summary>
    /// How a quest's completion is decided from its objectives. v1 ships only <see cref="AllRequired"/>; the enum
    /// reserves room for more rules (e.g. any-required / threshold) without a breaking API change.
    /// </summary>
    public enum QuestCompletionRule
    {
        /// <summary>The quest is Completed when every <c>Required</c> objective is Completed.</summary>
        AllRequired = 0
    }
}
