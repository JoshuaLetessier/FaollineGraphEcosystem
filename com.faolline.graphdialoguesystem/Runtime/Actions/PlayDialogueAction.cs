using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Starts a dialogue through <see cref="DialogueBus"/> when executed as an OnEnter action on a
    /// gameflow (or any graph) node. When the dialogue ends, the action raises a signal on the context
    /// so the node's <see cref="BaseNodeData.AwaitSignalName"/> can resume the flow automatically.
    /// <para>
    /// Usage: attach to a Statement node's OnEnter list, assign the <see cref="DialogueGraph"/>, and
    /// set the node's <c>AwaitSignalName</c> to the same <see cref="SignalDef"/> (or leave both empty
    /// for the auto-derived default <c>"dialogue_done_{GraphId}"</c>).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Play Dialogue", fileName = "PlayDialogueAction")]
    public sealed class PlayDialogueAction : BaseAction
    {
        [SerializeField, Tooltip("The DialogueGraph asset to play when this action executes.")]
        private DialogueGraph _dialogueGraph;
        [SerializeField, Tooltip("Signal name raised on the context when the dialogue ends. Empty = auto-derived from the graph's GraphId (dialogue_done_{GraphId}).")]
        private string _signalName;
        [SerializeField, Tooltip("When enabled, missing localization keys fall back to the authored node Title text.")]
        private bool _titleFallback = true;

        /// <summary>The dialogue to play.</summary>
        public DialogueGraph DialogueGraph { get => _dialogueGraph; set => _dialogueGraph = value; }

        /// <summary>Signal name raised when the dialogue ends. Empty = auto-derived from the graph's GraphId.</summary>
        public string SignalDef { get => _signalName; set => _signalName = value; }

        /// <summary>When true, missing localization keys fall back to authored node Title.</summary>
        public bool TitleFallback { get => _titleFallback; set => _titleFallback = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_dialogueGraph == null)
            {
                Debug.LogWarning("[GraphDialogue] PlayDialogueAction: no dialogue graph assigned; skipping.");
                return;
            }

            var signal = string.IsNullOrEmpty(_signalName)
                ? "dialogue_done_" + _dialogueGraph.GraphId
                : _signalName;

            DialogueBus.Play(
                _dialogueGraph,
                context,
                speakerLookup: _dialogueGraph.FindSpeaker,
                onEnded: _ => context.RaiseSignal(signal),
                titleFallback: _titleFallback);
        }
    }
}
