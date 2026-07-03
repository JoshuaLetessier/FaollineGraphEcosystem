using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphGameFlow;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Samples.GameFlowBridge
{
    /// <summary>
    /// Drop-in component for a flow-embedded dialogue: wires a <see cref="GraphFlowDriver"/> (running a
    /// flow whose SubGraph nodes embed dialogue graphs) to an <see cref="IDialogueView"/> through the
    /// same <see cref="DialoguePlaybackController"/> that <see cref="DialogueDriver"/> uses for standalone
    /// dialogues — same typewriter/auto-advance/choice-timeout/voice/history behaviour, same view.
    /// <para>
    /// Speakers can't be read off a single <see cref="DialogueGraph"/> asset here (the flow may traverse
    /// several dialogue subgraphs), so assign the full speaker set this scene needs directly.
    /// </para>
    /// </summary>
    public class FlowDialogueBridge : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField, Tooltip("The flow driver to bridge. Leave empty to use the persistent cross-scene GraphFlowDriver.Active at Awake.")]
        private GraphFlowDriver driver;
        [SerializeField, Tooltip("A component implementing IDialogueView (Canvas or UI Toolkit view).")]
        private MonoBehaviour viewBehaviour;
        [SerializeField, Tooltip("Every speaker used by any dialogue subgraph this flow can reach.")]
        private List<Speaker> speakers = new List<Speaker>();

        [Header("Flow")]
        [SerializeField, Tooltip("Automatically advance a line after it finishes displaying.")]
        private bool autoAdvance;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after a line finishes (typewriter included) before auto-advancing.")]
        private float autoAdvanceDelay = 2f;
        [SerializeField, Min(0f), Tooltip("Seconds before the first available choice is auto-selected. 0 = disabled.")]
        private float choiceTimeout;

        [Header("Audio")]
        [SerializeField, Tooltip("Optional AudioSource used to play each line's Voice Clip.")]
        private AudioSource voiceSource;

        private IDialogueView _view;
        private GraphFlowDialogueSource _source;
        private DialoguePlaybackController _controller;

        /// <summary>The active view (resolves once from <c>viewBehaviour</c>, like <see cref="DialogueDriver.View"/>).</summary>
        public IDialogueView View
        {
            get
            {
                if (_view != null) return _view;
                if (viewBehaviour is IDialogueView direct) _view = direct;
                else if (viewBehaviour != null) _view = viewBehaviour.GetComponent<IDialogueView>();
                return _view;
            }
        }

        private void Awake()
        {
            // A flow driver often lives in another scene (a persistent boot driver spanning scene
            // loads), which the inspector cannot reference — fall back to the active one.
            if (driver == null) driver = GraphFlowDriver.Active;
            if (driver == null)
            {
                Debug.LogError("[GraphDialogue] FlowDialogueBridge: no GraphFlowDriver assigned " +
                    "and no persistent GraphFlowDriver.Active to fall back to.");
                return;
            }

            View?.BindSpeakers(speakers);

            var localization = LocalizationContext.Current;
            var presenter = new DialoguePresenter(
                localization.Provider, localization.AssetProvider, FindSpeaker, localization.StrictMode);

            _source = new GraphFlowDialogueSource(driver, presenter);
            _controller = new DialoguePlaybackController(
                _source, () => View, autoAdvance, autoAdvanceDelay, choiceTimeout, voiceSource);
        }

        /// <summary>
        /// Advances the current line (completes the typewriter first, then steps) — wire a
        /// "Continue" button or an input handler here. Ignored while awaiting a choice, exactly
        /// like <see cref="DialoguePlaybackController.Advance"/>.
        /// </summary>
        public void Advance() => _controller?.Advance();

        private void Update() => _controller?.Tick(Time.time);

        private void OnDestroy()
        {
            _controller?.Teardown();
            _source?.Teardown();
        }

        private Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            foreach (var s in speakers)
                if (s != null && s.SpeakerId == speakerId) return s;
            return null;
        }
    }
}
