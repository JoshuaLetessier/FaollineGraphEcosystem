using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// <see cref="INodeExecutor"/> for <see cref="DialogueLineNodeData"/>. The runner calls
    /// <see cref="Execute"/> when a line node is entered; this executor records the entered line so the
    /// <see cref="DialoguePlayer"/> (and tests) can confirm which line is active. Text/speaker
    /// resolution itself is done by the player from the node + injected localization provider, keeping
    /// this executor free of any localization dependency.
    /// </summary>
    public sealed class DialogueLineExecutor : INodeExecutor
    {
        /// <inheritdoc/>
        public string NodeType => DialogueLineNodeData.NodeTypeId;

        /// <summary>The most recently entered line node, or null before any line is entered.</summary>
        public DialogueLineNodeData LastLine { get; private set; }

        /// <inheritdoc/>
        public void Execute(BaseNodeData node, BaseContext context)
        {
            if (node is DialogueLineNodeData line)
                LastLine = line;
        }
    }
}
