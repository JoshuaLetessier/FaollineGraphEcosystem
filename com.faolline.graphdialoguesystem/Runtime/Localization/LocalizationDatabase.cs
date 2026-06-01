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
        [SerializeField] private List<LocalizationKeyEntry> _speakerKeys = new();
        [SerializeField] private LocalizationDatabaseMetadata _metadata = new();

        public IReadOnlyList<LocalizationGraphEntry> Graphs => _graphs;

        /// <summary>
        /// Global speaker display-name keys, derived from each Speaker's SpeakerId at build time.
        /// These are NOT per-graph: speakers are shared across dialogues. Synced to the global
        /// Dialogue_Speakers collection by the provider.
        /// </summary>
        public IReadOnlyList<LocalizationKeyEntry> SpeakerKeys => _speakerKeys;

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

        /// <summary>Adds a global speaker display-name key (deduplicated).</summary>
        public void AddSpeakerKey(string key, string defaultHint = "")
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmed = key.Trim();
            if (_speakerKeys.Exists(k => k.Key == trimmed)) return;
            _speakerKeys.Add(new LocalizationKeyEntry { Key = trimmed, Type = LocalizationKeyType.SpeakerName, DefaultHint = defaultHint });
        }

        /// <summary>Clears all entries (used during rebuild).</summary>
        public void Clear()
        {
            _graphs.Clear();
            _speakerKeys.Clear();
        }

        /// <summary>Gets all unique keys across all graphs and speakers.</summary>
        public HashSet<string> GetAllKeys()
        {
            var keys = new HashSet<string>();
            foreach (var graph in _graphs)
            {
                foreach (var key in graph.Keys)
                    keys.Add(key.Key);
            }
            foreach (var speakerKey in _speakerKeys)
                keys.Add(speakerKey.Key);
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
        Text = 0,           // Line content (key derived via DialogueLocalizationKeys.ForLine)
        SpeakerName = 1,    // Speaker display name (key derived via DialogueLocalizationKeys.ForSpeaker)
        ChoiceLabel = 2,    // Choice option label (key derived via DialogueLocalizationKeys.ForChoice)
    }
}
