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

        /// <summary>The reason this graph execution path ended.</summary>
        public EndReason EndReason
        {
            get => _endReason;
            set => _endReason = value;
        }
    }
}
