namespace Faolline.GraphDialogue
{
    /// <summary>A spoken line ready for display: speaker + localized text + expression.</summary>
    public sealed class LineStep : DialogueStep
    {
        /// <summary>Logical speaker id from the node (may be empty).</summary>
        public string SpeakerId { get; }

        /// <summary>Speaker display name resolved into the active locale (or a fallback).</summary>
        public string ResolvedSpeakerName { get; }

        /// <summary>Line text resolved into the active locale (or a fallback).</summary>
        public string ResolvedText { get; }

        /// <summary>Requested speaker expression key (e.g. "neutral").</summary>
        public string ExpressionKey { get; }

        public LineStep(string nodeId, string speakerId, string resolvedSpeakerName,
                        string resolvedText, string expressionKey) : base(nodeId)
        {
            SpeakerId = speakerId ?? string.Empty;
            ResolvedSpeakerName = resolvedSpeakerName ?? string.Empty;
            ResolvedText = resolvedText ?? string.Empty;
            ExpressionKey = expressionKey ?? string.Empty;
        }
    }
}
