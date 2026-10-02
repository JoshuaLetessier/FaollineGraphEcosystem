using System;
using System.Collections.Generic;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Transient in-memory index of all localization keys found during a build pass. Created by the
    /// builder, passed to the exporter/syncer, then discarded. Not persisted — the CSV files and
    /// Unity Tables are the durable artifacts; this is the transfer object between scan and export.
    /// </summary>
    public class LocalizationDatabase
    {
        private readonly List<LocalizationGraphEntry> _graphs = new();
        private readonly List<LocalizationKeyEntry> _globalKeys = new();

        public IReadOnlyList<LocalizationGraphEntry> Graphs => _graphs;

        /// <summary>
        /// Global keys not tied to a specific graph (e.g. speaker display names).
        /// </summary>
        public IReadOnlyList<LocalizationKeyEntry> GlobalKeys => _globalKeys;

        public int TotalGraphsScanned { get; set; }
        public int TotalKeysFound { get; set; }

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

        /// <summary>
        /// Adds a global key (deduplicated by key string — the first add wins). <paramref name="group"/> picks the
        /// table it is built into (one table per group, see <see cref="LocalizationTableNames.ForGroup"/>); null or
        /// blank means the lib's default group. Group spellings are canonicalized: one that differs from an earlier
        /// group only by letter case, or by characters invalid in table names, reuses the earlier spelling (and is
        /// reported once) — otherwise the two would produce colliding tables on a case-insensitive file system.
        /// </summary>
        public void AddGlobalKey(string key, LocalizationKeyType type, string defaultHint = "", string group = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmed = key.Trim();
            if (_globalKeys.Exists(k => k.Key == trimmed)) return;
            _globalKeys.Add(new LocalizationKeyEntry { Key = trimmed, Type = type, DefaultHint = defaultHint, Group = CanonicalGroup(group) });
        }

        /// <summary>Global keys partitioned by group (null = default group), groups in first-appearance order.</summary>
        public IReadOnlyList<(string group, IReadOnlyList<LocalizationKeyEntry> keys)> GlobalKeysByGroup()
        {
            var order = new List<string>();
            var byGroup = new Dictionary<string, List<LocalizationKeyEntry>>();
            foreach (var entry in _globalKeys)
            {
                var id = entry.Group ?? string.Empty;
                if (!byGroup.TryGetValue(id, out var list))
                {
                    list = new List<LocalizationKeyEntry>();
                    byGroup[id] = list;
                    order.Add(id);
                }
                list.Add(entry);
            }

            var result = new List<(string, IReadOnlyList<LocalizationKeyEntry>)>(order.Count);
            foreach (var id in order) result.Add((id.Length == 0 ? null : id, byGroup[id]));
            return result;
        }

        private readonly Dictionary<string, string> _groupSpellings = new();
        private readonly HashSet<string> _reportedGroupVariants = new();

        private string CanonicalGroup(string group)
        {
            var normalized = LocalizationTableNames.NormalizeGroup(group);
            if (normalized == null) return null;

            var identity = LocalizationTableNames.GroupComparisonKey(normalized);
            if (!_groupSpellings.TryGetValue(identity, out var first))
            {
                _groupSpellings[identity] = normalized;
                return normalized;
            }

            if (first != normalized && _reportedGroupVariants.Add(normalized))
                Faolline.GraphLogging.Logging.Warning("GraphLocalization.Validation",
                    $"[LocalizationDatabase] Group '{normalized}' differs from group '{first}' only by letter case or by " +
                    $"characters invalid in table names, so both share the table of '{first}'. Use one spelling.");
            return first;
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
        private readonly List<LocalizationKeyEntry> _keys = new();

        public IReadOnlyList<LocalizationKeyEntry> Keys => _keys;

        public void AddKey(string key, LocalizationKeyType type, string nodeId = "", string defaultHint = "", int assetFlags = 0)
        {
            var existing = _keys.Find(k => k.Key == key && k.Type == type);
            if (existing == null)
                _keys.Add(new LocalizationKeyEntry { Key = key, Type = type, NodeId = nodeId, DefaultHint = defaultHint, AssetFlags = assetFlags });
            else
                existing.AssetFlags |= assetFlags;
        }
    }

    [Serializable]
    public class LocalizationKeyEntry
    {
        public string Key;
        public LocalizationKeyType Type;
        public string NodeId;
        public string DefaultHint;
        public int AssetFlags;

        /// <summary>
        /// Global keys only: the table group the key is built into (normalized; null = the lib's default group).
        /// </summary>
        public string Group;

        public bool HasLocalizedAsset => (AssetFlags & ~1) != 0;
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
