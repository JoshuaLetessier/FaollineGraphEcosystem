using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Indexed database of all localization keys found across DialogueGraphs.
    /// Acts as validator and cache between graphs (source) and provider (translations).
    /// Built by DialogueLocalizationBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Localization Database", fileName = "GraphDialogueLocalizationDatabase")]
    public class LocalizationDatabase : ScriptableObject
    {
        [SerializeField] private List<LocalizationGraphEntry> _graphs = new();
        [SerializeField] private LocalizationDatabaseMetadata _metadata = new();

        public IReadOnlyList<LocalizationGraphEntry> Graphs => _graphs;
        public LocalizationDatabaseMetadata Metadata => _metadata;

        /// <summary>Gets or creates entry for a graph (by GUID).</summary>
        public LocalizationGraphEntry GetOrCreateGraphEntry(string graphGuid, string graphName)
        {
            var existing = _graphs.Find(e => e.GraphGuid == graphGuid);
            if (existing != null) return existing;

            var entry = new LocalizationGraphEntry { GraphGuid = graphGuid, GraphName = graphName };
            _graphs.Add(entry);
            return entry;
        }

        /// <summary>Finds a graph entry by GUID.</summary>
        public LocalizationGraphEntry FindGraphEntry(string graphGuid) => _graphs.Find(e => e.GraphGuid == graphGuid);

        /// <summary>Clears all entries (used during rebuild).</summary>
        public void Clear() => _graphs.Clear();

        /// <summary>Gets all unique keys across all graphs.</summary>
        public HashSet<string> GetAllKeys()
        {
            var keys = new HashSet<string>();
            foreach (var graph in _graphs)
            {
                foreach (var key in graph.Keys)
                    keys.Add(key.Key);
            }
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
        public System.DateTime LastBuildTime = System.DateTime.MinValue;
        public int TotalGraphsScanned;
        public int TotalKeysFound;
        public int OrphansDetected;
    }

    public enum LocalizationKeyType
    {
        Text = 0,           // Line content (textKey)
        SpeakerName = 1,    // Speaker display name (speakerKey)
        ChoiceLabel = 2,    // Choice option label (displayTextKey)
    }
}
