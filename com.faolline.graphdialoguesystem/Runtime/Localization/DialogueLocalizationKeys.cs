using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Single source of truth for localization keys, derived deterministically from a node/choice/speaker
    /// identity. Used by BOTH the table builder (to create entries) and the runtime player (to resolve),
    /// so authors never type a key by hand — there is no string field that can drift or break.
    /// Format: a type prefix + the stable Id (node/choice GUID, or the speaker's logical id).
    /// </summary>
    public static class DialogueLocalizationKeys
    {
        public const string LinePrefix = "line_";
        public const string ChoicePrefix = "choice_";
        public const string SpeakerPrefix = "speaker_";

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
    }
}
