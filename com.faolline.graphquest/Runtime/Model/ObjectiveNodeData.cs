using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// One goal within a quest; a node of a <see cref="QuestGraph"/>. Its completion/fail are graphcore
    /// <see cref="BaseCondition"/>s evaluated against the shared context; its reward is a graphcore
    /// <see cref="BaseAction"/>. Append-only fields on the subclass — graphcore's <see cref="BaseNodeData"/> is
    /// untouched. Prerequisites to other objectives are <see cref="BaseEdgeData"/> edges in the owning graph.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveNodeData : BaseNodeData
    {
        /// <summary>Canonical node-type id for quest objectives.</summary>
        public const string NodeTypeId = "graphquest.objective";

        [Header("Completion")]
        [SerializeField, Tooltip("Condition checked against the context each evaluation. When it holds, the objective records Completed. Null = never auto-completes (manual only).")]
        private BaseCondition _completionCondition;
        [SerializeField, Tooltip("Optional. When it holds, the objective records Failed. Checked before completion (fail takes precedence).")]
        private BaseCondition _failCondition;
        [SerializeField, Tooltip("Required objectives gate quest completion; optional ones track state and award rewards only.")]
        private bool _required = true;

        [Header("Prerequisites")]
        [SerializeField, Tooltip("How many prerequisite objectives must be Completed to unlock this one (k-of-N).\n\n" +
            "-1 (default) = ALL (AND).\n" +
            "1 = ANY (OR).\n" +
            "0 or negative = no gate.\n" +
            "k > N = never unlocks from prerequisites.")]
        private int _requiredPrerequisiteCount = -1;

        [Header("Rewards")]
        [SerializeField, Tooltip("Optional action executed once when the objective enters Completed (e.g. grant XP, unlock an item).")]
        private BaseAction _reward;

        [Header("Timing")]
        [SerializeField, Tooltip("Time limit in seconds. Once active, the objective fails if not completed within this duration. 0 (default) = no limit. Only checked when the host calls Evaluate(now) with a clock.")]
        private float _timeLimitSeconds;

        [Header("Display")]
        [SerializeField, TextArea, Tooltip("Longer description for a quest journal or tracker UI. The short label is the inherited Title field.")]
        private string _description = string.Empty;

        /// <summary>When this holds against the context, the objective is recorded Completed. Null ⇒ never auto-completes.</summary>
        public BaseCondition CompletionCondition { get => _completionCondition; set => _completionCondition = value; }

        /// <summary>Optional. When it holds, the objective is recorded Failed. Checked before completion (fail &gt; complete).</summary>
        public BaseCondition FailCondition { get => _failCondition; set => _failCondition = value; }

        /// <summary>A required objective gates/decides its quest's completion; an optional one tracks state + rewards only. Default true.</summary>
        public bool Required { get => _required; set => _required = value; }

        /// <summary>Optional. Executed once when the objective enters Completed (the consumer supplies the effect).</summary>
        public BaseAction Reward { get => _reward; set => _reward = value; }

        /// <summary>
        /// How many prerequisites must be Completed for this objective to unlock (k-of-N). <c>-1</c> (the default)
        /// means ALL of them (AND). Otherwise: <c>1</c> = OR, <c>1 &lt; k &lt; N</c> = N-of-M, <c>k ≤ 0</c> = ungated,
        /// <c>k &gt; N</c> never unlocks from prerequisites. Backs graphstandard's <c>ReactiveEvaluator</c> k-of-N.
        /// </summary>
        public int RequiredPrerequisiteCount { get => _requiredPrerequisiteCount; set => _requiredPrerequisiteCount = value; }

        /// <summary>Optional longer description for a quest journal/tracker UI (the short label is the inherited <see cref="Faolline.GraphCore.BaseNodeData.Title"/>). Never null.</summary>
        public string Description { get => _description; set => _description = value ?? string.Empty; }

        /// <summary>
        /// Optional time limit in seconds: once this objective is Active, it Fails if not Completed within this many
        /// seconds of game time. <c>0</c> (the default) or negative means no limit. Timers are only checked when the
        /// host calls <c>QuestEvaluator.Evaluate(now)</c> with a clock; <c>Evaluate()</c> ignores them.
        /// </summary>
        public float TimeLimitSeconds { get => _timeLimitSeconds; set => _timeLimitSeconds = value; }
    }
}
