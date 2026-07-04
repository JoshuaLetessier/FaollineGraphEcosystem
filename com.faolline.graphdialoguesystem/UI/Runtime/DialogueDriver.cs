using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Drop-in component that plays a <see cref="DialogueGraph"/> through an <see cref="IDialogueView"/>.
    /// Owns a headless <see cref="DialoguePlayer"/>, forwards its steps to the view, and feeds the view's
    /// choice selection back to the player. Pointer interaction comes from the view's controls; keyboard
    /// input is added by the keyboard story. The driver is null-view safe (runs logically + warns).
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphdialoguesystem/README.md")]
    public class DialogueDriver : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private DialogueGraph graph;
        [SerializeField, Tooltip("A component implementing IDialogueView (Canvas or UI Toolkit view).")]
        private MonoBehaviour viewBehaviour;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private string locale = "en";

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

        [Header("Debug")]
        [SerializeField, Tooltip("Draws an on-screen OnGUI overlay (state, current node, line/choices) for dev.")]
        private bool showDebugOverlay;
        [SerializeField] private Vector2 overlayPosition = new Vector2(10, 10);

        // Resolved/injected collaborators (settable for tests before StartDialogue).
        private IDialogueView _view;
        private ILocalizationProvider _provider;
        // Optional programmatic override; when null, speakers come from the graph (graph.Speakers).
        private IReadOnlyList<Speaker> _speakersOverride;
        private DialoguePlayer _player;
        private DialoguePlaybackController _controller;

        /// <summary>
        /// The active view. Resolves from the assigned <c>viewBehaviour</c>: if that component is itself an
        /// <see cref="IDialogueView"/> it is used directly; otherwise an <see cref="IDialogueView"/> on the
        /// same GameObject is looked up (so dragging the Canvas/host object also works). Settable for tests.
        /// </summary>
        public IDialogueView View
        {
            get
            {
                if (_view != null) return _view;
                if (viewBehaviour is IDialogueView direct) _view = direct;
                else if (viewBehaviour != null) _view = viewBehaviour.GetComponent<IDialogueView>();
                return _view;
            }
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

        /// <summary>
        /// Raised when the dialogue gets stuck (no valid branch from the current node). Lets the game react
        /// (e.g. close the UI or show an error) instead of freezing silently.
        /// </summary>
        public event Action OnStuck;

        /// <summary>Raised for each line as it is shown — drives backlog/history UIs.</summary>
        public event Action<LineStep> OnLineShown;

        /// <summary>The lines shown so far this session, oldest first (backlog source).</summary>
        public IReadOnlyList<LineStep> History => _controller?.History ?? Array.Empty<LineStep>();

        // ── Lifecycle ───────────────────────────────────────────────────────────────

        private void OnValidate()
        {
            if (viewBehaviour != null && !(viewBehaviour is IDialogueView) &&
                viewBehaviour.GetComponent<IDialogueView>() == null)
            {
                Debug.LogWarning($"[GraphDialogue] DialogueDriver: assigned View Behaviour " +
                    $"'{viewBehaviour.GetType().Name}' is not an IDialogueView and none is on its GameObject. " +
                    "Assign a CanvasDialogueView or UIToolkitDialogueView.", this);
            }
        }

        private void Start()
        {
            if (autoStart && graph != null)
                StartDialogue(graph);
        }

        private void Update()
        {
            if (_player == null || _controller == null) return;

            if (_controller.AwaitingChoice)
            {
                int k = ReadChoiceDigit();
                if (k > 0) { ChooseByIndex(k); return; }
            }
            else if (ReadAdvance())
            {
                Advance();
                return;
            }

            _controller.Tick(Time.time);
        }

        private void OnDestroy() => Teardown();

        /// <summary>
        /// Optional programmatic override of the speaker set. When not set (or set to null), speakers come
        /// from the played graph (<see cref="DialogueGraph.Speakers"/>), so the scene needs no speaker list.
        /// </summary>
        public void SetSpeakers(IReadOnlyList<Speaker> value) => _speakersOverride = value;

        /// <summary>Speakers in effect: the explicit override if provided, else the graph's own speakers.</summary>
        public IReadOnlyList<Speaker> ActiveSpeakers =>
            _speakersOverride ?? (graph != null ? graph.Speakers : System.Array.Empty<Speaker>());

        internal void ConfigureFlowForTest(bool auto, float delay, float timeout)
        {
            autoAdvance = auto;
            autoAdvanceDelay = delay;
            choiceTimeout = timeout;
        }

        internal void ConfigureAudioForTest(AudioSource source) => voiceSource = source;

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
            var assets = LocalizationContext.Current.AssetProvider;

            View?.BindSpeakers(ActiveSpeakers);

            _player = new DialoguePlayer(graph, new DialogueContext(), provider, FindSpeaker, strict, assets);
            _controller = new DialoguePlaybackController(
                _player, () => View, autoAdvance, autoAdvanceDelay, choiceTimeout, voiceSource);
            _controller.OnStuck += HandleStuck;
            _controller.OnLineShown += step => OnLineShown?.Invoke(step);

            _player.Start();
        }

        /// <summary>Advances the current line. Ignored while awaiting a choice.</summary>
        public void Advance() => _controller?.Advance();

        /// <summary>Selects an option by its routing id.</summary>
        public void Choose(string choiceId) => _controller?.Choose(choiceId);

        /// <summary>
        /// Selects the option at <paramref name="oneBasedIndex"/> in the currently displayed choices
        /// (as a keyboard 1–9 press would). No-op when not at a choice, out of range, or unavailable.
        /// </summary>
        public void ChooseByIndex(int oneBasedIndex) => _controller?.ChooseByIndex(oneBasedIndex);

        /// <summary>Steps back one entry (player history).</summary>
        public void Back() => _player?.Back();

        /// <summary>Steps back to the last checkpoint.</summary>
        public void BackToCheckpoint() => _player?.BackToCheckpoint();

        private void HandleStuck()
        {
            Debug.LogWarning("[GraphDialogue] DialogueDriver: dialogue is stuck (no valid branch from the " +
                "current node). Check your edge/choice conditions.", this);
            OnStuck?.Invoke();
        }

        // ── Debug overlay (optional dev aid) ───────────────────────────────────────────

        private void OnGUI()
        {
            if (!showDebugOverlay || _player == null || _controller == null) return;

            const float w = 560f, h = 20f;
            float x = overlayPosition.x, y = overlayPosition.y;
            var title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

            string state = _controller.Ended ? "Ended" : _controller.AwaitingChoice ? "ChoiceReady" : "LineReady";
            GUI.Label(new Rect(x, y, w, h), "DialogueDriver (debug)", title); y += h;
            GUI.Label(new Rect(x, y, w, h), $"State: {state}  |  Node: {_player.CurrentStep?.NodeId}"); y += h;

            if (_controller.LastLine != null)
            {
                var line = _controller.LastLine;
                GUI.Label(new Rect(x, y, w, h), $"LINE — {line.ResolvedSpeakerName}: \"{line.ResolvedText}\""); y += h;
                GUI.Label(new Rect(x, y, w, h), "[Space]/click to advance"); y += h;
            }
            else if (_controller.AwaitingChoice && _controller.LastChoices != null)
            {
                var choices = _controller.LastChoices;
                GUI.Label(new Rect(x, y, w, h), "CHOICES — press [1..9] or click:"); y += h;
                for (int i = 0; i < choices.Options.Count; i++)
                {
                    var o = choices.Options[i];
                    var tag = o.Available ? "" : "  [blocked]";
                    GUI.Label(new Rect(x + 12, y, w, h), $"{i + 1}) {o.ResolvedLabel}{tag}"); y += h;
                }
            }
            else if (_controller.Ended)
            {
                GUI.Label(new Rect(x, y, w, h), "END"); y += h;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            foreach (var s in ActiveSpeakers)
                if (s != null && s.SpeakerId == speakerId) return s;
            return null;
        }

        // ── Input (pointer always works via the view's buttons; keyboard is a convenience) ──────────

#if ENABLE_INPUT_SYSTEM
        private static bool ReadAdvance()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        private static int ReadChoiceDigit()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 5;
            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 6;
            if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 7;
            if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 8;
            if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 9;
            return 0;
        }
#else
        private static bool ReadAdvance() => Input.GetKeyDown(KeyCode.Space);

        private static int ReadChoiceDigit()
        {
            for (int k = 1; k <= 9; k++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + k) || Input.GetKeyDown(KeyCode.Keypad0 + k)) return k;
            return 0;
        }
#endif

        private void Teardown()
        {
            if (_controller != null)
            {
                _controller.OnStuck -= HandleStuck;
                _controller.Teardown();
                _controller = null;
            }
            _player?.DetachEditorProbe();
            _player = null;
        }
    }
}
