using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Marks the end of a graph execution path. Carries the reason the path terminated.
    /// </summary>
    [Serializable]
    public class EndNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for end nodes.</summary>
        public const string NodeTypeId = "graphcore/end";

        [SerializeField]
        private EndReason _endReason = EndReason.Completed;

        [SerializeField, Tooltip("Optional semantic label to distinguish multiple End nodes that share the same End Reason (e.g. \"persuaded\", \"rejected\"). Surfaced in the EndStep so the consumer can branch on the outcome without reading context flags.")]
        private string _outcomeLabel = string.Empty;

        /// <summary>The reason this graph execution path ended.</summary>
        public EndReason EndReason
        {
            get => _endReason;
            set => _endReason = value;
        }

        /// <summary>
        /// Optional semantic label distinguishing multiple end nodes that share the same
        /// <see cref="EndReason"/> (e.g. "persuaded", "rejected"). Empty by default.
        /// </summary>
        public string OutcomeLabel
        {
            get => _outcomeLabel;
            set => _outcomeLabel = value ?? string.Empty;
        }
    }
}
