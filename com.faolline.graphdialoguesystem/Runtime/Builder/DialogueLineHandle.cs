namespace Faolline.GraphDialogue
{
    /// <summary>A <see cref="DialogueLineNodeData"/> handle: set the spoken text and the speaker expression.</summary>
    public sealed class DialogueLineHandle : DialogueNodeHandle<DialogueLineHandle>
    {
        private readonly DialogueLineNodeData _line;

        internal DialogueLineHandle(DialogueGraphBuilder owner, DialogueLineNodeData node) : base(owner, node)
            => _line = node;

        /// <summary>Sets the line's source text (the node <c>Title</c> the localization pipeline derives from).</summary>
        public DialogueLineHandle Say(string text)
        {
            _line.Title = text ?? string.Empty;
            return this;
        }

        /// <summary>Sets the speaker's expression key (defaults to "neutral").</summary>
        public DialogueLineHandle Expression(string key)
        {
            _line.ExpressionKey = key;
            return this;
        }
    }
}
