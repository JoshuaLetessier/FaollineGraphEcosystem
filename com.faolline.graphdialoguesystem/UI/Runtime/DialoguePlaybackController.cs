using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphDialogue;
using Faolline.GraphLogging;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Source-agnostic playback/UI glue shared by <see cref="DialogueDriver"/> (standalone,
    /// <see cref="DialoguePlayer"/>-backed) and any flow-embedded bridge (presenter-backed
    /// <see cref="IDialoguePlaybackSource"/>). Owns typewriter-skip-on-advance, the auto-advance timer,
    /// choice timeout, voice playback, and line history — everything that used to live directly on
    /// <see cref="DialogueDriver"/> — so both entry points get identical UI behaviour from one
    /// implementation. Not a <c>MonoBehaviour</c>: the host component calls <see cref="Tick"/> from its
    /// own <c>Update</c>.
    /// </summary>
    public sealed class DialoguePlaybackController
    {
        private readonly IDialoguePlaybackSource _source;
        private readonly Func<IDialogueView> _viewProvider;
        private readonly bool _autoAdvance;
        private readonly float _autoAdvanceDelay;
        private readonly float _choiceTimeout;
        private readonly AudioSource _voiceSource;

        private bool _awaitingChoice;
        private LineStep _lastLine;
        private ChoiceStep _lastChoices;
        private bool _ended;
        private float _lineShownTime;
        private float _choiceShownTime;
        private bool _autoAdvanceArmed;

        private readonly List<LineStep> _history = new List<LineStep>();

        /// <summary>Raised when the dialogue gets stuck (no valid branch from the current node).</summary>
        public event Action OnStuck;

        /// <summary>Raised for each line as it is shown — drives backlog/history UIs.</summary>
        public event Action<LineStep> OnLineShown;

        /// <summary>The lines shown so far this session, oldest first (backlog source).</summary>
        public IReadOnlyList<LineStep> History => _history;

        /// <summary>True while awaiting a choice pick.</summary>
        public bool AwaitingChoice => _awaitingChoice;

        /// <summary>True once the dialogue (or dialogue segment) has ended.</summary>
        public bool Ended => _ended;

        /// <summary>The most recently shown line, or null when not currently on a line.</summary>
        public LineStep LastLine => _lastLine;

        /// <summary>The most recently shown choices, or null when not currently on a choice.</summary>
        public ChoiceStep LastChoices => _lastChoices;

        private IDialogueView View => _viewProvider();

        /// <param name="source">The step source (a <see cref="DialoguePlayer"/> or a flow-embedded adapter).</param>
        /// <param name="viewProvider">Resolves the active view lazily (mirrors <see cref="DialogueDriver.View"/>'s
        /// late-binding so views assigned after construction still work).</param>
        public DialoguePlaybackController(
            IDialoguePlaybackSource source,
            Func<IDialogueView> viewProvider,
            bool autoAdvance,
            float autoAdvanceDelay,
            float choiceTimeout,
            AudioSource voiceSource)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _viewProvider = viewProvider ?? throw new ArgumentNullException(nameof(viewProvider));
            _autoAdvance = autoAdvance;
            _autoAdvanceDelay = autoAdvanceDelay;
            _choiceTimeout = choiceTimeout;
            _voiceSource = voiceSource;

            _source.OnLine += HandleLine;
            _source.OnChoices += HandleChoices;
            _source.OnEnded += HandleEnded;
            _source.OnStuck += HandleStuck;

            if (View != null)
                View.ChoiceSelected += Choose;
            else
                Logging.Warning("GraphDialogue", "[GraphDialogue] DialoguePlaybackController: no IDialogueView assigned — running logic only.");
        }

        /// <summary>Detaches from the source/view. Call when the host component is destroyed or rebinds.</summary>
        public void Teardown()
        {
            _source.OnLine -= HandleLine;
            _source.OnChoices -= HandleChoices;
            _source.OnEnded -= HandleEnded;
            _source.OnStuck -= HandleStuck;
            var view = View;
            if (view != null) view.ChoiceSelected -= Choose;
        }

        /// <summary>Call once per frame with elapsed time — drives the auto-advance timer, choice timeout,
        /// and keyboard-free advance polling normally done by the host's own input handling.</summary>
        public void Tick(float time)
        {
            if (_awaitingChoice)
            {
                if (_choiceTimeout > 0f && time - _choiceShownTime >= _choiceTimeout)
                    SelectFirstAvailableChoice();
                return;
            }

            bool typing = View is DialogueViewBase vb && vb.IsTyping;
            if (typing) _lineShownTime = time;
            else if (_autoAdvanceArmed && _lastLine != null && time - _lineShownTime >= _autoAdvanceDelay)
            {
                _autoAdvanceArmed = false;
                Advance();
            }
        }

        /// <summary>Advances the current line. Ignored while awaiting a choice.</summary>
        public void Advance()
        {
            if (_awaitingChoice) return;
            // While the line is still revealing, the first advance completes it instead of skipping ahead.
            if (View is DialogueViewBase vb && vb.IsTyping) { vb.SkipTyping(); return; }
            _source.Advance();
        }

        /// <summary>Selects an option by its routing id.</summary>
        public void Choose(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return;
            _source.Choose(choiceId);
        }

        /// <summary>
        /// Selects the option at <paramref name="oneBasedIndex"/> in the currently displayed choices
        /// (as a keyboard 1–9 press would). No-op when not at a choice, out of range, or unavailable.
        /// </summary>
        public void ChooseByIndex(int oneBasedIndex)
        {
            if (!_awaitingChoice || _lastChoices == null) return;
            var options = _lastChoices.Options;
            if (oneBasedIndex < 1 || oneBasedIndex > options.Count) return;
            var option = options[oneBasedIndex - 1];
            if (option == null || !option.Available) return;
            Choose(option.ChoiceId);
        }

        private void SelectFirstAvailableChoice()
        {
            if (_lastChoices == null) return;
            foreach (var o in _lastChoices.Options)
                if (o != null && o.Available) { Choose(o.ChoiceId); return; }
        }

        private void HandleLine(LineStep step)
        {
            _awaitingChoice = false;
            _lastChoices = null;
            _lastLine = step;
            _ended = false;
            _lineShownTime = Time.time;
            _autoAdvanceArmed = _autoAdvance;
            if (step != null) { _history.Add(step); OnLineShown?.Invoke(step); }
            PlayVoice(step);
            View?.ShowLine(step);
        }

        private void PlayVoice(LineStep step)
        {
            if (_voiceSource == null) return;
            if (_voiceSource.isPlaying) _voiceSource.Stop();
            if (step?.VoiceClip != null)
            {
                _voiceSource.clip = step.VoiceClip;
                _voiceSource.Play();
            }
        }

        private void StopVoice()
        {
            if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        }

        private void HandleChoices(ChoiceStep step)
        {
            _awaitingChoice = true;
            _lastChoices = step;
            _lastLine = null;
            _ended = false;
            _autoAdvanceArmed = false;
            _choiceShownTime = Time.time;
            View?.ShowChoices(step);
        }

        private void HandleEnded(EndStep step)
        {
            _awaitingChoice = false;
            _lastChoices = null;
            _lastLine = null;
            _ended = true;
            _autoAdvanceArmed = false;
            StopVoice();
            View?.HideAll();
        }

        private void HandleStuck()
        {
            _awaitingChoice = false;
            _lastChoices = null;
            _lastLine = null;
            _autoAdvanceArmed = false;
            StopVoice();
            OnStuck?.Invoke();
        }
    }
}
