using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Marks the entry point of a graph. Every graph must have exactly one start node.
    /// </summary>
    [Serializable]
    public class StartNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for start nodes.</summary>
        public const string NodeTypeId = "graphcore/start";
    }
}
