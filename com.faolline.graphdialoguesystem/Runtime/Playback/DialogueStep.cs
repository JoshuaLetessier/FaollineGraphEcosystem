namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Base type for the discrete steps a <see cref="DialoguePlayer"/> emits during playback.
    /// Concrete steps: <see cref="LineStep"/>, <see cref="ChoiceStep"/>, <see cref="EndStep"/>.
    /// </summary>
    public abstract class DialogueStep
    {
        /// <summary>Id of the node that produced this step.</summary>
        public string NodeId { get; }

        protected DialogueStep(string nodeId) => NodeId = nodeId ?? string.Empty;
    }
}
