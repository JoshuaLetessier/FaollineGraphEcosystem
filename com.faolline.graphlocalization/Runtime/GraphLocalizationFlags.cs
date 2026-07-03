using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Per-graph localization metadata: the graph-level default <see cref="LocalizedAssetFlags"/> plus per-node
    /// overrides (which asset kinds — Text, Audio, Image, … — each node localizes). Embedded directly on a graph
    /// asset that implements <see cref="ILocalizedGraph"/> (the same self-contained extension pattern a
    /// <c>DialogueGraph</c> uses for its speaker list), so there is no separate companion asset to manage.
    /// </summary>
    [Serializable]
    public sealed class GraphLocalizationFlags
    {
        [SerializeField] private LocalizedAssetFlags _defaultFlags = LocalizedAssetFlags.Text;
        [SerializeField] private List<NodeLocalizationEntry> _entries = new();

        private Dictionary<string, int> _indexCache;

        /// <summary>The graph-level default applied to any node without an explicit override.</summary>
        public LocalizedAssetFlags DefaultFlags
        {
            get => _defaultFlags;
            set => _defaultFlags = value;
        }

        /// <summary>The per-node overrides (in insertion order).</summary>
        public IReadOnlyList<NodeLocalizationEntry> Entries => _entries;

        /// <summary>The flags for <paramref name="nodeId"/> — its override, else the graph default.</summary>
        public LocalizedAssetFlags GetFlags(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return _defaultFlags;
            var idx = FindIndex(nodeId);
            return idx >= 0 ? _entries[idx].Flags : _defaultFlags;
        }

        /// <summary>Sets (or adds) the override for <paramref name="nodeId"/>. No-op for a null/empty id.</summary>
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

        /// <summary>True when <paramref name="nodeId"/> localizes more than plain text (Audio, Image, …).</summary>
        public bool HasLocalizedAssets(string nodeId)
        {
            var flags = GetFlags(nodeId);
            return flags != LocalizedAssetFlags.None && flags != LocalizedAssetFlags.Text;
        }

        /// <summary>Replaces all overrides with the current default for every id in <paramref name="nodeIds"/>.</summary>
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

    /// <summary>A single per-node localization-flags override.</summary>
    [Serializable]
    public sealed class NodeLocalizationEntry
    {
        public string NodeId;
        public LocalizedAssetFlags Flags = LocalizedAssetFlags.Text;
    }
}
