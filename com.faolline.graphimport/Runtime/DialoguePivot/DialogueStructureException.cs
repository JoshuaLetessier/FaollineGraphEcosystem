using System;

namespace Faolline.GraphImport
{
    public enum DialogueStructureIssue
    {
        DuplicateNodeId,
        DanglingNext,
        InvalidEntryNode
    }

    /// <summary>
    /// Raised when an interchange dialogue's own structure is broken — a dangling "next" reference,
    /// a duplicate node id, or an entry point matching no node. Always identifies the dialogue (and
    /// the node, when relevant); never silently dropped or guessed past (FR-006).
    /// </summary>
    public sealed class DialogueStructureException : Exception
    {
        public string DialogueId { get; }
        public string NodeId { get; }
        public DialogueStructureIssue Issue { get; }

        public DialogueStructureException(string dialogueId, string nodeId, DialogueStructureIssue issue)
            : base(BuildMessage(dialogueId, nodeId, issue))
        {
            DialogueId = dialogueId;
            NodeId = nodeId;
            Issue = issue;
        }

        static string BuildMessage(string dialogueId, string nodeId, DialogueStructureIssue issue)
        {
            switch (issue)
            {
                case DialogueStructureIssue.DuplicateNodeId:
                    return $"Dialogue '{dialogueId}': duplicate node id '{nodeId}'.";
                case DialogueStructureIssue.DanglingNext:
                    return $"Dialogue '{dialogueId}': node '{nodeId}' references a 'next' node that doesn't exist in this dialogue.";
                default:
                    return $"Dialogue '{dialogueId}': entryNodeId '{nodeId}' doesn't match any node in this dialogue.";
            }
        }
    }
}
