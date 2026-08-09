using System;

namespace Faolline.GraphImport
{
    public enum DialogueReferenceReason
    {
        NotFound,
        Ambiguous
    }

    /// <summary>Raised when a sub-dialogue link's target can't be resolved to exactly one dialogue within the interchange set.</summary>
    public sealed class DialogueReferenceException : Exception
    {
        public string FromDialogueId { get; }
        public string FromNodeId { get; }
        public string RawValue { get; }
        public DialogueReferenceReason Reason { get; }

        public DialogueReferenceException(string fromDialogueId, string fromNodeId, string rawValue, DialogueReferenceReason reason)
            : base(reason == DialogueReferenceReason.NotFound
                ? $"Dialogue '{fromDialogueId}' node '{fromNodeId}': sub-dialogue target '{rawValue}' did not match any dialogue in the set."
                : $"Dialogue '{fromDialogueId}' node '{fromNodeId}': sub-dialogue target '{rawValue}' matched more than one dialogue in the set.")
        {
            FromDialogueId = fromDialogueId;
            FromNodeId = fromNodeId;
            RawValue = rawValue;
            Reason = reason;
        }
    }

    /// <summary>Raised when sub-dialogue links form a cycle (a dialogue reaching itself again through a chain of references).</summary>
    public sealed class DialogueCycleException : Exception
    {
        public string DialogueId { get; }

        public DialogueCycleException(string dialogueId)
            : base($"Dialogue '{dialogueId}' is part of a sub-dialogue reference cycle.")
        {
            DialogueId = dialogueId;
        }
    }
}
