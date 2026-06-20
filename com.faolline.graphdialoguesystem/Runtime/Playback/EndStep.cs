using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Signals the dialogue has ended, carrying the reason and an optional semantic outcome.</summary>
    public sealed class EndStep : DialogueStep
    {
        /// <summary>Why the dialogue ended.</summary>
        public EndReason EndReason { get; }

        /// <summary>
        /// Semantic label set on the <see cref="EndNodeData"/> (e.g. "persuaded", "rejected").
        /// Empty when no label was set.
        /// </summary>
        public string OutcomeLabel { get; }

        public EndStep(string nodeId, EndReason endReason, string outcomeLabel = null) : base(nodeId)
        {
            EndReason = endReason;
            OutcomeLabel = outcomeLabel ?? string.Empty;
        }
    }
}
