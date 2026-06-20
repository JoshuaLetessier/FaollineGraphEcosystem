namespace Faolline.GraphQuest
{
    /// <summary>
    /// Deterministic localization keys for quest/objective identity, mirroring the dialogue
    /// pattern (<see cref="Faolline.GraphDialogue.DialogueLocalizationKeys"/>). Keys are derived
    /// from quest/objective ids — never typed by hand, never drift.
    /// </summary>
    public static class QuestLocalizationKeys
    {
        public const string QuestPrefix = "quest_";
        public const string ObjectivePrefix = "objective_";
        public const string DescriptionSuffix = "_desc";

        public static string ForQuest(string questId)
            => string.IsNullOrEmpty(questId) ? string.Empty : QuestPrefix + questId;

        public static string ForQuestDescription(string questId)
            => string.IsNullOrEmpty(questId) ? string.Empty : QuestPrefix + questId + DescriptionSuffix;

        public static string ForObjective(string objectiveId)
            => string.IsNullOrEmpty(objectiveId) ? string.Empty : ObjectivePrefix + objectiveId;

        public static string ForObjectiveDescription(string objectiveId)
            => string.IsNullOrEmpty(objectiveId) ? string.Empty : ObjectivePrefix + objectiveId + DescriptionSuffix;
    }
}
