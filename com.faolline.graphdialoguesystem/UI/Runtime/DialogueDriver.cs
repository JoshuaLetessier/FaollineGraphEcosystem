using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Drop-in component that plays a <see cref="DialogueGraph"/> through an <see cref="IDialogueView"/>.
    /// Owns a headless <see cref="DialoguePlayer"/>, forwards its steps to the view, and feeds the view's
    /// choice selection back to the player. Pointer interaction comes from the view's controls; keyboard
    /// input is added by the keyboard story. The driver is null-view safe (runs logically + warns).
    /// </summary>
    public class DialogueDriver : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private DialogueGraph graph;
        [SerializeField, Tooltip("A component implementing IDialogueView (Canvas or UI Toolkit view).")]
        private MonoBehaviour viewBehaviour;
        [SerializeField] private List<Speaker> speakers = new List<Speaker>();
        [SerializeField] private bool autoStart = true;
        [SerializeField] private string locale = "en";

        // Resolved/injected collaborators (settable for tests before StartDialogue).
        private IDialogueView _view;
        private ILocalizationProvider _provider;
        private DialoguePlayer _player;
        private bool _awaitingChoice;

        /// <summary>The active view. Defaults to the assigned <c>viewBehaviour</c>; settable for tests.</summary>
        public IDialogueView View
        {
            get => _view ??= viewBehaviour as IDialogueView;
            set => _view = value;
        }

        /// <summary>Localization provider used to build the player. Defaults to <see cref="LocalizationContext"/>.</summary>
        public ILocalizationProvider Provider
        {
            get => _provider;
            set => _provider = value;
        }

        /// <summary>The underlying player (null before <see cref="StartDialogue"/>).</summary>
        public DialoguePlayer Player => _player;

        // ── Lifecycle ───────────────────────────────────────────────────────────────

        private void Start()
        {
            if (autoStart && graph != null)
                StartDialogue(graph);
        }

        private void OnDestroy() => Teardown();

        /// <summary>Replaces the speaker set (used by tests and runtime reconfiguration).</summary>
        public void SetSpeakers(IReadOnlyList<Speaker> value)
        {
            speakers.Clear();
            if (value != null) speakers.AddRange(value);
        }

        // ── Control surface ─────────────────────────────────────────────────────────

        /// <summary>(Re)starts playback of <paramref name="dialogueGraph"/> from the beginning.</summary>
        public void StartDialogue(DialogueGraph dialogueGraph)
        {
            if (dialogueGraph == null)
            {
                Debug.LogError("[GraphDialogue] DialogueDriver: graph is null.");
                return;
            }
            graph = dialogueGraph;

            Teardown();

            var provider = _provider ?? LocalizationContext.Current.Provider;
            var strict = LocalizationContext.Current.StrictMode;

            View?.BindSpeakers(speakers);

            _player = new DialoguePlayer(graph, new DialogueContext(), provider, FindSpeaker, strict);
            _player.OnLine += HandleLine;
            _player.OnChoices += HandleChoices;
            _player.OnEnded += HandleEnded;

            if (View != null)
                View.ChoiceSelected += Choose;
            else
                Debug.LogWarning("[GraphDialogue] DialogueDriver: no IDialogueView assigned — running logic only.");

            _awaitingChoice = false;
            _player.Start();
        }

        /// <summary>Advances the current line. Ignored while awaiting a choice.</summary>
        public void Advance()
        {
            if (_player == null || _awaitingChoice) return;
            _player.Advance();
        }

        /// <summary>Selects an option by its routing id.</summary>
        public void Choose(string choiceId)
        {
            if (_player == null || string.IsNullOrEmpty(choiceId)) return;
            _player.Choose(choiceId);
        }

        /// <summary>Steps back one entry (player history).</summary>
        public void Back() => _player?.Back();

        /// <summary>Steps back to the last checkpoint.</summary>
        public void BackToCheckpoint() => _player?.BackToCheckpoint();

        // ── Player event handlers ────────────────────────────────────────────────────

        private void HandleLine(LineStep step)
        {
            _awaitingChoice = false;
            View?.ShowLine(step);
        }

        private void HandleChoices(ChoiceStep step)
        {
            _awaitingChoice = true;
            View?.ShowChoices(step);
        }

        private void HandleEnded(EndStep step)
        {
            _awaitingChoice = false;
            View?.HideAll();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            foreach (var s in speakers)
                if (s != null && s.SpeakerId == speakerId) return s;
            return null;
        }

        private void Teardown()
        {
            if (_player != null)
            {
                _player.OnLine -= HandleLine;
                _player.OnChoices -= HandleChoices;
                _player.OnEnded -= HandleEnded;
                _player = null;
            }
            if (_view != null) _view.ChoiceSelected -= Choose;
        }
    }
}
