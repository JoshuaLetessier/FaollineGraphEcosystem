using System;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Ambient, single-active dialogue relay. <see cref="Play"/> starts a <see cref="DialoguePlayer"/>
    /// and relays its steps (line, choice, end, stuck) as static events so any UI can subscribe once
    /// without holding a reference to the player. Input methods (<see cref="Advance"/>,
    /// <see cref="Choose"/>, <see cref="RaiseSignal"/>, <see cref="Tick"/>) route to the active player.
    /// <para>Only one dialogue at a time — starting a new one force-stops the previous.</para>
    /// </summary>
    public static class DialogueBus
    {
        private static DialoguePlayer _player;
        private static Action<EndStep> _onEndedCallback;

        /// <summary>The currently playing player, or null when idle.</summary>
        public static DialoguePlayer ActivePlayer => _player;

        /// <summary>True while a dialogue is playing.</summary>
        public static bool IsPlaying => _player != null;

        /// <summary>Fires when a new dialogue starts playing.</summary>
        public static event Action<DialogueGraph> OnDialogueStarted;

        /// <summary>Relayed from the active player: a spoken line is ready.</summary>
        public static event Action<LineStep> OnLine;

        /// <summary>Relayed from the active player: a choice point is ready.</summary>
        public static event Action<ChoiceStep> OnChoices;

        /// <summary>Relayed from the active player: the dialogue ended.</summary>
        public static event Action<EndStep> OnEnded;

        /// <summary>Relayed from the active player: no valid branch (stuck).</summary>
        public static event Action OnStuck;

        /// <summary>
        /// Starts a dialogue through the bus. If one is already playing, it is force-stopped first.
        /// <paramref name="onEnded"/> is an optional callback invoked when this specific dialogue ends
        /// (e.g. to raise a signal on the flow context).
        /// </summary>
        public static void Play(
            DialogueGraph graph,
            BaseContext context,
            Func<string, Speaker> speakerLookup = null,
            Action<EndStep> onEnded = null,
            bool titleFallback = true)
        {
            if (graph == null)
            {
                UnityEngine.Debug.LogWarning("[GraphDialogue] DialogueBus.Play: null graph; ignored.");
                return;
            }

            if (_player != null)
            {
                UnityEngine.Debug.LogWarning("[GraphDialogue] DialogueBus.Play: stopping previous dialogue.");
                StopInternal();
            }

            _onEndedCallback = onEnded;
            var dialogueContext = context as DialogueContext ?? new DialogueContext();

            _player = new DialoguePlayer(graph, dialogueContext, speakerLookup: speakerLookup, titleFallback: titleFallback);
            _player.OnLine += HandleLine;
            _player.OnChoices += HandleChoices;
            _player.OnEnded += HandleEnded;
            _player.OnStuck += HandleStuck;

            OnDialogueStarted?.Invoke(graph);
            _player.Start();
        }

        /// <summary>Advances past the current line. No-op when idle.</summary>
        public static void Advance() => _player?.Advance();

        /// <summary>Selects a choice option. No-op when idle.</summary>
        public static void Choose(string choiceId) => _player?.Choose(choiceId);

        /// <summary>Raises a named signal into the active dialogue. No-op when idle.</summary>
        public static void RaiseSignal(string name) => _player?.RaiseSignal(name);

        /// <summary>Feeds elapsed time to the active dialogue. No-op when idle.</summary>
        public static void Tick(float deltaSeconds) => _player?.Tick(deltaSeconds);

        /// <summary>Force-stops the active dialogue, clearing the bus. No-op when idle.</summary>
        public static void Stop() => StopInternal();

        private static void HandleLine(LineStep step) => OnLine?.Invoke(step);
        private static void HandleChoices(ChoiceStep step) => OnChoices?.Invoke(step);
        private static void HandleStuck() => OnStuck?.Invoke();

        private static void HandleEnded(EndStep step)
        {
            var callback = _onEndedCallback;
            Cleanup();
            OnEnded?.Invoke(step);
            callback?.Invoke(step);
        }

        private static void StopInternal()
        {
            if (_player == null) return;
            Cleanup();
        }

        private static void Cleanup()
        {
            if (_player != null)
            {
                _player.OnLine -= HandleLine;
                _player.OnChoices -= HandleChoices;
                _player.OnEnded -= HandleEnded;
                _player.OnStuck -= HandleStuck;
            }
            _player = null;
            _onEndedCallback = null;
        }
    }
}
