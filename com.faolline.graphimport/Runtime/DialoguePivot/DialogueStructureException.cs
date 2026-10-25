using System;

namespace Faolline.GraphImport
{
    public enum DialogueStructureIssue
    {
        DuplicateNodeId,
        DanglingNext,
        InvalidEntryNode,
        MissingOptionId,
        DuplicateOptionId
    }

    /// <summary>
    /// Raised when an interchange dialogue's own structure is broken — a dangling "next" reference,
    /// a duplicate node id, an entry point matching no node, or a choice option with a missing or
    /// duplicate id. Always identifies the dialogue (and the node, when relevant; the option, for
    /// option issues); never silently dropped or guessed past (FR-006).
    /// </summary>
    public sealed class DialogueStructureException : Exception
    {
        public string DialogueId { get; }
        public string NodeId { get; }
        /// <summary>The offending option's id, for <see cref="DialogueStructureIssue.DuplicateOptionId"/>; null otherwise.</summary>
        public string OptionId { get; }
        public DialogueStructureIssue Issue { get; }

        public DialogueStructureException(string dialogueId, string nodeId, DialogueStructureIssue issue, string optionId = null)
            : base(BuildMessage(dialogueId, nodeId, issue, optionId))
        {
            DialogueId = dialogueId;
            NodeId = nodeId;
            OptionId = optionId;
            Issue = issue;
        }

        static string BuildMessage(string dialogueId, string nodeId, DialogueStructureIssue issue, string optionId)
        {
            switch (issue)
            {
                case DialogueStructureIssue.DuplicateNodeId:
                    return $"Dialogue '{dialogueId}': duplicate node id '{nodeId}'.";
                case DialogueStructureIssue.DanglingNext:
                    return $"Dialogue '{dialogueId}': node '{nodeId}' references a 'next' node that doesn't exist in this dialogue.";
                case DialogueStructureIssue.MissingOptionId:
                    return $"Dialogue '{dialogueId}': choice node '{nodeId}' has an option without an 'id' — its localization key would be unpredictable.";
                case DialogueStructureIssue.DuplicateOptionId:
                    return $"Dialogue '{dialogueId}': choice node '{nodeId}' reuses option id '{optionId}', already used in this dialogue.";
                default:
                    return $"Dialogue '{dialogueId}': entryNodeId '{nodeId}' doesn't match any node in this dialogue.";
            }
        }
    }
}
