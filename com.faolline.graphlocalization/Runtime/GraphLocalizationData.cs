using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Per-graph companion asset storing localization metadata (asset flags per node + graph-level
    /// default). Lives beside the graph asset it describes. Created automatically by the editor
    /// tooling when localization flags are configured.
    /// </summary>
    public sealed class GraphLocalizationData : ScriptableObject
    {
        [SerializeField] private string _graphGuid = string.Empty;
        [SerializeField] private LocalizedAssetFlags _defaultFlags = LocalizedAssetFlags.Text;
        [SerializeField] private List<NodeLocalizationEntry> _entries = new();

        private Dictionary<string, int> _indexCache;

        public string GraphGuid { get => _graphGuid; set => _graphGuid = value; }

        public LocalizedAssetFlags DefaultFlags
        {
            get => _defaultFlags;
            set => _defaultFlags = value;
        }

        public IReadOnlyList<NodeLocalizationEntry> Entries => _entries;

        public LocalizedAssetFlags GetFlags(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return _defaultFlags;
            var idx = FindIndex(nodeId);
            return idx >= 0 ? _entries[idx].Flags : _defaultFlags;
        }

        public void SetFlags(string nodeId, LocalizedAssetFlags flags)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            var idx = FindIndex(nodeId);
            if (idx >= 0)
                _entries[idx].Flags = flags;
            else
            {
                _entries.Add(new NodeLocalizationEntry { NodeId = nodeId, Flags = flags });
                _indexCache = null;
            }
        }

        public bool HasLocalizedAssets(string nodeId)
        {
            var flags = GetFlags(nodeId);
            return flags != LocalizedAssetFlags.None && flags != LocalizedAssetFlags.Text;
        }

        public void ApplyDefaultToAll(IEnumerable<string> nodeIds)
        {
            _entries.Clear();
            _indexCache = null;
            if (nodeIds == null) return;
            foreach (var id in nodeIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                _entries.Add(new NodeLocalizationEntry { NodeId = id, Flags = _defaultFlags });
            }
        }

        private int FindIndex(string nodeId)
        {
            if (_indexCache == null)
            {
                _indexCache = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i] != null && !string.IsNullOrEmpty(_entries[i].NodeId))
                        _indexCache[_entries[i].NodeId] = i;
            }
            return _indexCache.TryGetValue(nodeId, out var index) ? index : -1;
        }
    }

    [Serializable]
    public sealed class NodeLocalizationEntry
    {
        public string NodeId;
        public LocalizedAssetFlags Flags = LocalizedAssetFlags.Text;
    }
}
