using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Headless playback facade over graphcore's <see cref="BaseRunner"/>. Drives a
    /// <see cref="DialogueGraph"/> and re-emits dialogue-domain steps: localized <see cref="LineStep"/>,
    /// <see cref="ChoiceStep"/> (options with availability), and <see cref="EndStep"/>. Pass-through
    /// nodes (start, generic statement, sub-dialogue) are advanced automatically; the player pauses on
    /// line and choice nodes until <see cref="Advance"/> / <see cref="Choose"/> is called.
    /// <para>No <c>MonoBehaviour</c>, no scene — fully testable in EditMode.</para>
    /// </summary>
    public sealed class DialoguePlayer
    {
        private const int MaxDrainSteps = 1000;

        private readonly DialogueGraph _graph;
        private readonly DialogueContext _context;
        private readonly ILocalizationProvider _localization;
        private readonly ILocalizedAssetProvider _assets;
        private readonly Func<string, Speaker> _speakerLookup;
        private readonly LocalizationStrictMode _strictMode;
        private readonly List<string> _missingKeys = new List<string>();

        private readonly BaseRunner _runner = new BaseRunner();
        private NodeExecutorRegistry _registry;

        private bool _stuck;
        private bool _ended;
        private EndReason _endReason = EndReason.Completed;

        /// <summary>Raised when a spoken line is ready (player paused awaiting <see cref="Advance"/>).</summary>
        public event Action<LineStep> OnLine;

        /// <summary>Raised when a choice point is ready (player paused awaiting <see cref="Choose"/>).</summary>
        public event Action<ChoiceStep> OnChoices;

        /// <summary>Raised once when the dialogue ends.</summary>
        public event Action<EndStep> OnEnded;

        /// <summary>Raised when no valid branch is available (stuck).</summary>
        public event Action OnStuck;

        /// <summary>Raised once per distinct missing key (Audit/Strict modes), as it is first encountered.</summary>
        public event Action<string> OnMissingKey;

        /// <summary>Distinct localization keys that failed to resolve during this session (audit log).</summary>
        public IReadOnlyList<string> MissingKeys => _missingKeys;

        /// <summary>The most recently emitted step, or null before <see cref="Start"/>.</summary>
        public DialogueStep CurrentStep { get; private set; }

        /// <summary>The underlying runner state.</summary>
        public RunnerState State => _runner.State;

        public DialoguePlayer(
            DialogueGraph graph,
            DialogueContext context,
            ILocalizationProvider localization,
            Func<string, Speaker> speakerLookup = null,
            LocalizationStrictMode strictMode = LocalizationStrictMode.Permissive,
            ILocalizedAssetProvider assets = null)
        {
            _graph = graph;
            _context = context ?? new DialogueContext();
            _localization = localization ?? new CsvLocalizationProvider(string.Empty, "en");
            _assets = assets;
            _speakerLookup = speakerLookup;
            _strictMode = strictMode;

            _runner.OnEnded += reason =>
            {
                _ended = true;
                _endReason = reason;
                var node = _runner.CurrentNode;
                CurrentStep = new EndStep(node != null ? node.Id : string.Empty, reason);
                OnEnded?.Invoke((EndStep)CurrentStep);
            };
            _runner.OnStuck += () => _stuck = true;
        }

        /// <summary>
        /// Starts playback at the graph's entry node and emits the first step. Logs a diagnostic and
        /// does nothing harmful when the graph has no entry node or contains an immediate cycle.
        /// </summary>
        public void Start()
        {
            if (_graph == null)
            {
                Debug.LogError("[GraphDialogue] Cannot start: graph is null.");
                return;
            }

            _stuck = false;
            _ended = false;
            _context.InitFromGraph(_graph);
            _registry = DialogueExecutorRegistryFactory.Create();

            try
            {
                _runner.Start(_graph, _context, _registry);
            }
            catch (GraphCycleException ex)
            {
                Debug.LogError($"[GraphDialogue] Cycle detected: {ex.CyclicGraphId}. Playback aborted.");
                return;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[GraphDialogue] Cannot start dialogue: {ex.Message}");
                return;
            }

            Drain();
        }

        // ── Save / Restore ────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the current playback position and context into a <see cref="DialogueSessionState"/>
        /// that can be persisted (JSON) and used later to restore the session via
        /// <see cref="RestoreFrom"/>. Returns null when no step has been emitted yet.
        /// <para>
        /// Best called when <see cref="CurrentStep"/> is a <see cref="LineStep"/> on a checkpoint node
        /// (<c>IsCheckpoint == true</c>), but any active step is accepted.
        /// </para>
        /// </summary>
        public DialogueSessionState SaveState(string graphGuid = "")
        {
            if (CurrentStep == null) return null;
            return DialogueSessionState.Capture(graphGuid, CurrentStep.NodeId, _context);
        }

        /// <summary>
        /// Resumes playback from a previously saved <see cref="DialogueSessionState"/>.
        /// The saved context values are applied before the node is re-entered, so enter-actions
        /// that read context will see the restored values.
        /// </summary>
        public void RestoreFrom(DialogueSessionState state)
        {
            if (state == null)
            {
                Debug.LogError("[GraphDialogue] RestoreFrom: state is null.");
                return;
            }

            _stuck = false;
            _ended = false;
            _missingKeys.Clear();

            // Apply saved context BEFORE entering the node so enter-actions see restored values.
            state.ApplyContext(_context);

            _registry = DialogueExecutorRegistryFactory.Create();

            try
            {
                _runner.StartFrom(_graph, state.NodeId, _context, _registry);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GraphDialogue] RestoreFrom failed: {ex.Message}");
                return;
            }

            Drain();
        }

        /// <summary>Advances past the current line (linear). No-op unless paused on a line.</summary>
        public void Advance()
        {
            if (_runner.State != RunnerState.NodeReady) return;
            _runner.Proceed();
            Drain();
        }

        /// <summary>
        /// Selects the choice with <paramref name="choiceId"/> and continues. No-op when not paused at a
        /// choice or when the option is unavailable (its condition fails).
        /// </summary>
        public void Choose(string choiceId)
        {
            if (_runner.State != RunnerState.NodeReady) return;
            if (!(_runner.CurrentNode is ChoiceNodeData choiceNode)) return;
            if (!IsChoiceAvailable(choiceNode, choiceId)) return;
            // (availability uses the live _context)

            _runner.ChooseById(choiceId);
            Drain();
        }

        /// <summary>Steps back one entry, restoring prior context values.</summary>
        public void Back()
        {
            _stuck = false;
            _ended = false;
            _runner.GoBack();
            EmitForCurrentNode();
        }

        /// <summary>Steps back to the most recent checkpoint node.</summary>
        public void BackToCheckpoint()
        {
            _stuck = false;
            _ended = false;
            _runner.GoBackToCheckpoint();
            EmitForCurrentNode();
        }

        // ── Drain / emit ──────────────────────────────────────────────────────

        private void Drain()
        {
            int guard = 0;
            while (_runner.State == RunnerState.NodeReady && !_stuck && guard++ < MaxDrainSteps)
            {
                var node = _runner.CurrentNode;
                if (node is ChoiceNodeData || node is DialogueLineNodeData)
                {
                    EmitForCurrentNode();
                    return;
                }

                // Pass-through node (start / generic statement / sub-dialogue boundary): advance.
                // Entering a cyclic sub-dialogue raises GraphCycleException from the runner — convert it
                // to a safe diagnostic + stuck outcome (FR-020) instead of propagating.
                try
                {
                    _runner.Proceed();
                }
                catch (GraphCycleException ex)
                {
                    Debug.LogError($"[GraphDialogue] Cycle detected entering sub-dialogue: {ex.CyclicGraphId}. Playback stopped.");
                    _stuck = true;
                    break;
                }
            }

            if (_stuck)
                OnStuck?.Invoke();
        }

        private void EmitForCurrentNode()
        {
            if (_ended) return;
            var node = _runner.CurrentNode;
            if (node == null) return;

            if (node is DialogueLineNodeData line)
            {
                var step = BuildLineStep(line);
                CurrentStep = step;
                OnLine?.Invoke(step);
            }
            else if (node is ChoiceNodeData choice)
            {
                var step = BuildChoiceStep(choice);
                CurrentStep = step;

                // No selectable option (none defined, or all condition-gated to unavailable) → stuck,
                // rather than presenting a dead end (spec edge case).
                bool anyAvailable = false;
                foreach (var opt in step.Options) { if (opt.Available) { anyAvailable = true; break; } }
                if (!anyAvailable)
                {
                    _stuck = true;
                    OnStuck?.Invoke();
                    return;
                }

                OnChoices?.Invoke(step);
            }
        }

        private LineStep BuildLineStep(DialogueLineNodeData line)
        {
            string text = ResolveChecked(DialogueLocalizationKeys.ForLine(line));
            text = DialogueTextInterpolator.Interpolate(text, _context);
            string speakerName = ResolveSpeakerName(line.SpeakerKey);

            // Voice is resolved by the line's key from the localized asset tables (no per-node clip).
            var voice = _assets != null
                ? _assets.ResolveAsset<AudioClip>(DialogueLocalizationKeys.ForLine(line))
                : null;

            return new LineStep(line.Id, line.SpeakerKey, speakerName, text, line.ExpressionKey, voice);
        }

        private ChoiceStep BuildChoiceStep(ChoiceNodeData choiceNode)
        {
            var options = new List<ChoiceOption>();
            foreach (var baseChoice in choiceNode.Choices)
            {
                if (baseChoice == null) continue;
                string labelKey = DialogueLocalizationKeys.ForChoice(baseChoice);
                string label = string.IsNullOrEmpty(labelKey)
                    ? baseChoice.Id
                    : ResolveChecked(labelKey);
                label = DialogueTextInterpolator.Interpolate(label, _context);
                bool available = baseChoice.Condition == null || baseChoice.Condition.Evaluate(_context);
                options.Add(new ChoiceOption(baseChoice.Id, label, available));
            }
            return new ChoiceStep(choiceNode.Id, options);
        }

        /// <summary>
        /// Resolves a key through the provider and applies the configured <see cref="LocalizationStrictMode"/>
        /// when the key is missing (provider returns the <c>#key</c> fallback or empty):
        /// Permissive returns the fallback silently; Audit warns + records it; Strict throws.
        /// </summary>
        private string ResolveChecked(string key)
        {
            var locale = _localization.CurrentLocale;
            var value = _localization.Resolve(key, locale);

            bool missing = string.IsNullOrEmpty(value) || value == $"#{key}";
            if (!missing) return value;

            switch (_strictMode)
            {
                case LocalizationStrictMode.Strict:
                    throw new LocalizationException(key, locale);
                case LocalizationStrictMode.Audit:
                    if (!_missingKeys.Contains(key))
                    {
                        _missingKeys.Add(key);
                        Debug.LogWarning($"[GraphDialogue] Missing localization key '{key}' for locale '{locale}'.");
                        OnMissingKey?.Invoke(key);
                    }
                    break;
                // Permissive: return the fallback silently.
            }
            return value;
        }

        private string ResolveSpeakerName(string speakerKey)
        {
            if (string.IsNullOrEmpty(speakerKey)) return string.Empty;
            var speaker = _speakerLookup?.Invoke(speakerKey);
            if (speaker == null) return speakerKey;

            var nameKey = DialogueLocalizationKeys.ForSpeaker(speaker);
            if (!string.IsNullOrEmpty(nameKey))
            {
                var resolved = _localization.Resolve(nameKey, _localization.CurrentLocale);
                if (!string.IsNullOrEmpty(resolved) && resolved != $"#{nameKey}")
                    return resolved;
            }
            return string.IsNullOrEmpty(speaker.DisplayNameFallback) ? speakerKey : speaker.DisplayNameFallback;
        }

        private bool IsChoiceAvailable(ChoiceNodeData node, string choiceId)
        {
            foreach (var choice in node.Choices)
            {
                if (choice == null || choice.Id != choiceId) continue;
                return choice.Condition == null || choice.Condition.Evaluate(_context);
            }
            return false;
        }
    }
}
