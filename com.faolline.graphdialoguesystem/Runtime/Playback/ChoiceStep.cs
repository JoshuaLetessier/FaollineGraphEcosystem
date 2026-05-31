using System.Collections.Generic;

namespace Faolline.GraphDialogue
{
    /// <summary>A branch point presenting an ordered list of <see cref="ChoiceOption"/>.</summary>
    public sealed class ChoiceStep : DialogueStep
    {
        /// <summary>The options to present, in choice order. Never null.</summary>
        public IReadOnlyList<ChoiceOption> Options { get; }

        public ChoiceStep(string nodeId, IReadOnlyList<ChoiceOption> options) : base(nodeId)
        {
            Options = options ?? new List<ChoiceOption>();
        }
    }
}
