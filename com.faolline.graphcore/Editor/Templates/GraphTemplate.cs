using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// A reusable template of graph nodes and internal edges, persisted as a ScriptableObject asset.
    /// Created via "Save Selection as Template" in the graph editor canvas; inserted via the
    /// "Templates" contextual menu. On insertion, all node/edge IDs are regenerated (fresh GUIDs)
    /// and positions are offset to the insertion point. Conditions and actions are referenced, not
    /// copied — the template points to the same SO assets as the original nodes.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphCore/Graph Template", fileName = "NewGraphTemplate")]
    public class GraphTemplate : ScriptableObject
    {
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private List<string> _nodeJsons = new List<string>();
        [SerializeField] private List<string> _edgeJsons = new List<string>();

        /// <summary>Optional description of what this template contains.</summary>
        public string Description { get => _description; set => _description = value ?? string.Empty; }

        /// <summary>JSON-serialized BaseNodeData entries.</summary>
        public IReadOnlyList<string> NodeJsons => _nodeJsons;

        /// <summary>JSON-serialized BaseEdgeData entries (internal edges only).</summary>
        public IReadOnlyList<string> EdgeJsons => _edgeJsons;

        /// <summary>Number of nodes in this template.</summary>
        public int NodeCount => _nodeJsons.Count;

        /// <summary>
        /// Captures nodes and their internal edges into this template. Edges whose both endpoints
        /// are in <paramref name="nodes"/> are kept; edges to external nodes are dropped.
        /// Positions are stored relative to the selection's centroid (so insertion can place them
        /// at the cursor).
        /// </summary>
        public void Capture(IReadOnlyList<BaseNodeData> nodes, IReadOnlyList<BaseEdgeData> edges)
        {
            _nodeJsons.Clear();
            _edgeJsons.Clear();
            if (nodes == null || nodes.Count == 0) return;

            var nodeIds = new HashSet<string>();
            var centroid = Vector2.zero;
            foreach (var n in nodes)
            {
                if (n == null) continue;
                nodeIds.Add(n.Id);
                centroid += n.Position;
            }
            centroid /= nodes.Count;

            foreach (var n in nodes)
            {
                if (n == null) continue;
                var origPos = n.Position;
                n.Position -= centroid;
                _nodeJsons.Add(JsonUtility.ToJson(n));
                n.Position = origPos;
            }

            if (edges != null)
                foreach (var e in edges)
                    if (e != null && nodeIds.Contains(e.FromNodeId) && nodeIds.Contains(e.ToNodeId))
                        _edgeJsons.Add(JsonUtility.ToJson(e));
        }

        /// <summary>
        /// Instantiates the template's nodes and edges with fresh GUIDs, positioned around
        /// <paramref name="insertPosition"/>. Returns the ID remapping (old → new) so the
        /// caller can wire connections to the inserted nodes.
        /// </summary>
        public TemplateInsertResult Instantiate(Vector2 insertPosition)
        {
            var result = new TemplateInsertResult();
            if (_nodeJsons.Count == 0) return result;

            foreach (var json in _nodeJsons)
            {
                var placeholder = JsonUtility.FromJson<IdPlaceholder>(json);
                if (placeholder == null || string.IsNullOrEmpty(placeholder.Id)) continue;
                var newId = Guid.NewGuid().ToString("D");
                result.IdMap[placeholder.Id] = newId;
            }

            foreach (var json in _nodeJsons)
                result.NodeJsons.Add(json);

            foreach (var json in _edgeJsons)
                result.EdgeJsons.Add(json);

            result.InsertPosition = insertPosition;
            return result;
        }

        [Serializable]
        private class IdPlaceholder { public string Id; }
    }

    /// <summary>Result of <see cref="GraphTemplate.Instantiate"/>: raw JSON + ID remapping, ready for the
    /// graph view to deserialize and add to the canvas.</summary>
    public class TemplateInsertResult
    {
        public Dictionary<string, string> IdMap = new Dictionary<string, string>();
        public List<string> NodeJsons = new List<string>();
        public List<string> EdgeJsons = new List<string>();
        public Vector2 InsertPosition;
    }
}
