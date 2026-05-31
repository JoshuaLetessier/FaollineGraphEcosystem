using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Signals the dialogue has ended, carrying the reason.</summary>
    public sealed class EndStep : DialogueStep
    {
        /// <summary>Why the dialogue ended.</summary>
        public EndReason EndReason { get; }

        public EndStep(string nodeId, EndReason endReason) : base(nodeId)
        {
            EndReason = endReason;
        }
    }
}
