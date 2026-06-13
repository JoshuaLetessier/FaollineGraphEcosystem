using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A choice option just added via <see cref="DialogueChoiceHandle.Option"/>: route it to its target node
    /// (an edge keyed by the option's id) and optionally (re)set its gating condition.
    /// </summary>
    public sealed class DialogueOptionHandle
    {
        private readonly DialogueChoiceHandle _choiceHandle;
        private readonly DialogueChoice _option;

        internal DialogueOptionHandle(DialogueChoiceHandle choiceHandle, DialogueChoice option)
        {
            _choiceHandle = choiceHandle;
            _option = option;
        }

        /// <summary>Routes this option to <paramref name="target"/> (edge port = the option id), returning the
        /// owning choice handle so more options can be chained.</summary>
        public DialogueChoiceHandle To(DialogueNodeHandle target)
        {
            _choiceHandle.Owner.Connect(_choiceHandle, target, _option.Id);
            return _choiceHandle;
        }

        /// <summary>Sets the option's gating condition (only available when it passes).</summary>
        public DialogueOptionHandle When(BaseCondition condition)
        {
            _option.Condition = condition;
            return this;
        }
    }
}
