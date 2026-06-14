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

        [SerializeField] private BaseCondition _completionCondition;
        [SerializeField] private BaseCondition _failCondition;
        [SerializeField] private bool _required = true;
        [SerializeField] private BaseAction _reward;
        [SerializeField] private int _requiredPrerequisiteCount = -1;
        [SerializeField, TextArea] private string _description = string.Empty;

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
    }
}
