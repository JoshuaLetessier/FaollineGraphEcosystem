using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Represents a single intermediate statement in the graph.
    /// </summary>
    [Serializable]
    public class StatementNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for statement nodes.</summary>
        public const string NodeTypeId = "graphcore/statement";
    }
}
