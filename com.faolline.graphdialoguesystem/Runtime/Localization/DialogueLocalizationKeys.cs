using Faolline.GraphCore;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Single source of truth for localization keys, derived deterministically from a node/choice/speaker
    /// identity. Used by BOTH the table builder (to create entries) and the runtime player (to resolve),
    /// so authors never type a key by hand — there is no string field that can drift or break.
    /// Format: a type prefix + the stable Id (node/choice GUID, or the speaker's logical id).
    /// Also derives the table each key lives in (same rule as the build), so the runtime can look a key up
    /// in its own table instead of searching every table.
    /// </summary>
    public static class DialogueLocalizationKeys
    {
        public const string LinePrefix = "line_";
        public const string ChoicePrefix = "choice_";
        public const string SpeakerPrefix = "speaker_";

        /// <summary>The lib name the localization build files this lib's tables under.</summary>
        public const string LibName = "GraphDialogue";

        private const string SpeakersGroup = "Speakers";

        /// <summary>Localization key for a dialogue line's spoken text. Empty when the node has no Id.</summary>
        public static string ForLine(BaseNodeData node)
            => node == null || string.IsNullOrEmpty(node.Id) ? string.Empty : LinePrefix + node.Id;

        /// <summary>Localization key for a choice's displayed label. Empty when the choice has no Id.</summary>
        public static string ForChoice(BaseChoice choice)
            => choice == null || string.IsNullOrEmpty(choice.Id) ? string.Empty : ChoicePrefix + choice.Id;

        /// <summary>Localization key for a speaker's display name, derived from its logical SpeakerId.</summary>
        public static string ForSpeaker(Speaker speaker)
            => speaker == null ? string.Empty : ForSpeakerId(speaker.SpeakerId);

        /// <summary>Localization key for a speaker display name from a logical speaker id.</summary>
        public static string ForSpeakerId(string speakerId)
            => string.IsNullOrEmpty(speakerId) ? string.Empty : SpeakerPrefix + speakerId;

        /// <summary>
        /// The localization group a speaker's name is built into: <c>Speakers</c> when it has no
        /// <see cref="Speaker.LocalizationGroup"/>, else <c>Speakers_{group}</c> (trimmed).
        /// </summary>
        public static string SpeakerTableGroup(Speaker speaker)
            => SpeakerTableGroupOf(speaker != null ? speaker.LocalizationGroup : null);

        /// <summary>
        /// Table (base name) holding a speaker's display name: <c>GraphDialogue_Speakers</c> or
        /// <c>GraphDialogue_Speakers_{group}</c>.
        /// </summary>
        public static string ForSpeakerTable(Speaker speaker)
            => LocalizationTableNames.ForGroup(LibName, SpeakerTableGroup(speaker));

        /// <summary>
        /// Table (base name) a speaker with <see cref="Speaker.LocalizationGroup"/> = <paramref name="localizationGroup"/>
        /// would use — e.g. to preview it before the value is applied.
        /// </summary>
        public static string ForSpeakerGroupTable(string localizationGroup)
            => LocalizationTableNames.ForGroup(LibName, SpeakerTableGroupOf(localizationGroup));

        private static string SpeakerTableGroupOf(string localizationGroup)
        {
            var group = LocalizationTableNames.NormalizeGroup(localizationGroup);
            return group == null ? SpeakersGroup : SpeakersGroup + "_" + group;
        }

        /// <summary>Table (base name) holding a graph's line texts and choice labels. Null for a null graph.</summary>
        public static string ForGraphTable(BaseGraph graph)
            => graph == null ? null : LocalizationTableNames.ForGraph(graph.name);
    }
}
