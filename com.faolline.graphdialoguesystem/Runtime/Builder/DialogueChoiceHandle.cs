using System;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>A <see cref="ChoiceNodeData"/> handle: add selectable options, each routed to a target.</summary>
    public sealed class DialogueChoiceHandle : DialogueNodeHandle<DialogueChoiceHandle>
    {
        private readonly ChoiceNodeData _choice;

        internal DialogueChoiceHandle(DialogueGraphBuilder owner, ChoiceNodeData node) : base(owner, node)
            => _choice = node;

        /// <summary>
        /// Adds a selectable option labelled <paramref name="label"/> (the source text), optionally gated by
        /// <paramref name="condition"/>. Returns a handle whose <c>To(...)</c> routes the option to a target node.
        /// </summary>
        public DialogueOptionHandle Option(string label, BaseCondition condition = null)
        {
            var option = new DialogueChoice
            {
                Id = Guid.NewGuid().ToString("D"),
                Title = label ?? string.Empty,
                Condition = condition
            };
            _choice.Choices.Add(option);
            return new DialogueOptionHandle(this, option);
        }
    }
}
