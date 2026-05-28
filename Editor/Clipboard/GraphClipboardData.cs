using System;
using System.Collections.Generic;

namespace Faolline.GraphCore.Editor
{
    /// <summary>Intermediate clipboard model for copy/paste operations.</summary>
    [Serializable]
    public class GraphClipboardData
    {
        /// <summary>JSON-serialized BaseNodeData entries, one per copied node.</summary>
        public List<string> Nodes = new List<string>();

        /// <summary>JSON-serialized BaseEdgeData entries for intra-selection edges only.</summary>
        public List<string> Edges = new List<string>();
    }
}
