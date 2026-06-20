using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Indexed database of all localization keys found across graphs, built by the localization builder.
    /// Acts as validator and cache between graphs (source of truth) and provider (translations).
    /// One asset per lib, stored under Assets/Resources by convention.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphLocalization/Localization Database", fileName = "LocalizationDatabase")]
    public class LocalizationDatabase : ScriptableObject
    {
        [SerializeField] private List<LocalizationGraphEntry> _graphs = new();
        [SerializeField] private List<LocalizationKeyEntry> _globalKeys = new();
        [SerializeField] private LocalizationDatabaseMetadata _metadata = new();

        public IReadOnlyList<LocalizationGraphEntry> Graphs => _graphs;

        /// <summary>
        /// Global keys not tied to a specific graph (e.g. speaker display names).
        /// Synced to a shared collection by the provider.
        /// </summary>
        public IReadOnlyList<LocalizationKeyEntry> GlobalKeys => _globalKeys;

        public LocalizationDatabaseMetadata Metadata => _metadata;

        /// <summary>Gets or creates the entry for a graph identified by GUID.</summary>
        public LocalizationGraphEntry GetOrCreateGraphEntry(string graphGuid, string graphName)
        {
            var existing = _graphs.Find(e => e.GraphGuid == graphGuid);
            if (existing != null) return existing;

            var entry = new LocalizationGraphEntry { GraphGuid = graphGuid, GraphName = graphName };
            _graphs.Add(entry);
            return entry;
        }

        /// <summary>Finds a graph entry by GUID, or null.</summary>
        public LocalizationGraphEntry FindGraphEntry(string graphGuid) => _graphs.Find(e => e.GraphGuid == graphGuid);

        /// <summary>Adds a global key (deduplicated by key string).</summary>
        public void AddGlobalKey(string key, LocalizationKeyType type, string defaultHint = "")
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmed = key.Trim();
            if (_globalKeys.Exists(k => k.Key == trimmed)) return;
            _globalKeys.Add(new LocalizationKeyEntry { Key = trimmed, Type = type, DefaultHint = defaultHint });
        }

        /// <summary>Clears all entries (used at the start of each rebuild).</summary>
        public void Clear()
        {
            _graphs.Clear();
            _globalKeys.Clear();
        }

        /// <summary>Gets all unique keys across graphs and global keys.</summary>
        public HashSet<string> GetAllKeys()
        {
            var keys = new HashSet<string>();
            foreach (var graph in _graphs)
                foreach (var key in graph.Keys)
                    keys.Add(key.Key);
            foreach (var key in _globalKeys)
                keys.Add(key.Key);
            return keys;
        }
    }

    [Serializable]
    public class LocalizationGraphEntry
    {
        public string GraphGuid;
        public string GraphName;
        [SerializeField] private List<LocalizationKeyEntry> _keys = new();

        public IReadOnlyList<LocalizationKeyEntry> Keys => _keys;

        public void AddKey(string key, LocalizationKeyType type, string nodeId = "", string defaultHint = "")
        {
            var existing = _keys.Find(k => k.Key == key && k.Type == type);
            if (existing == null)
                _keys.Add(new LocalizationKeyEntry { Key = key, Type = type, NodeId = nodeId, DefaultHint = defaultHint });
        }

        public void Clear() => _keys.Clear();
    }

    [Serializable]
    public class LocalizationKeyEntry
    {
        public string Key;
        public LocalizationKeyType Type;
        public string NodeId;
        public string DefaultHint;
    }

    [Serializable]
    public class LocalizationDatabaseMetadata
    {
        public DateTime LastBuildTime = DateTime.MinValue;
        public int TotalGraphsScanned;
        public int TotalKeysFound;
        public int OrphansDetected;
    }

    public enum LocalizationKeyType
    {
        Text = 0,
        SpeakerName = 1,
        ChoiceLabel = 2,
        QuestName = 3,
        ObjectiveName = 4,
        ObjectiveDescription = 5,
    }
}
