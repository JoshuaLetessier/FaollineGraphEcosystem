using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

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
        private readonly Func<string, Speaker> _speakerLookup;

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

        /// <summary>The most recently emitted step, or null before <see cref="Start"/>.</summary>
        public DialogueStep CurrentStep { get; private set; }

        /// <summary>The underlying runner state.</summary>
        public RunnerState State => _runner.State;

        public DialoguePlayer(
            DialogueGraph graph,
            DialogueContext context,
            ILocalizationProvider localization,
            Func<string, Speaker> speakerLookup = null)
        {
            _graph = graph;
            _context = context ?? new DialogueContext();
            _localization = localization ?? new CsvLocalizationProvider(string.Empty, "en");
            _speakerLookup = speakerLookup;

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
            string text = _localization.Resolve(line.TextKey, _localization.CurrentLocale);
            string speakerName = ResolveSpeakerName(line.SpeakerKey);
            return new LineStep(line.Id, line.SpeakerKey, speakerName, text, line.ExpressionKey);
        }

        private ChoiceStep BuildChoiceStep(ChoiceNodeData choiceNode)
        {
            var options = new List<ChoiceOption>();
            foreach (var baseChoice in choiceNode.Choices)
            {
                if (baseChoice == null) continue;
                string labelKey = (baseChoice as DialogueChoice)?.DisplayTextKey ?? string.Empty;
                string label = string.IsNullOrEmpty(labelKey)
                    ? baseChoice.Id
                    : _localization.Resolve(labelKey, _localization.CurrentLocale);
                bool available = baseChoice.Condition == null || baseChoice.Condition.Evaluate(_context);
                options.Add(new ChoiceOption(baseChoice.Id, label, available));
            }
            return new ChoiceStep(choiceNode.Id, options);
        }

        private string ResolveSpeakerName(string speakerKey)
        {
            if (string.IsNullOrEmpty(speakerKey)) return string.Empty;
            var speaker = _speakerLookup?.Invoke(speakerKey);
            if (speaker == null) return speakerKey;

            if (!string.IsNullOrEmpty(speaker.DisplayNameKey))
            {
                var resolved = _localization.Resolve(speaker.DisplayNameKey, _localization.CurrentLocale);
                if (!string.IsNullOrEmpty(resolved) && resolved != $"#{speaker.DisplayNameKey}")
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
