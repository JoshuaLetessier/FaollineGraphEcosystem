#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// <see cref="ILocalizedAssetProvider"/> backed by Unity Localization **Asset Tables**. Resolves an
    /// asset by the same key as the text, for the selected locale.
    /// <para>
    /// <b>Targeted lookup</b> (<see cref="ResolveAssetInTable{T}"/>): reads only the designated table's own asset
    /// collections (<c>{table}_Audio</c>, <c>{table}_Sprite</c>…). A table the manifest knows but that has no asset
    /// collection resolves to null without opening anything; a table the manifest doesn't know at all falls back to
    /// the classic lookup, reported once per table.
    /// </para>
    /// <para>
    /// <b>Classic lookup</b> (<see cref="ResolveAsset{T}"/>): searches the manifest's asset collections in order,
    /// caching key → collection.
    /// </para>
    /// Returns null when the key is unknown or the asset is unassigned for the active locale. Gated so projects
    /// without com.unity.localization take no dependency.
    /// </summary>
    public sealed class UnityLocalizedAssetProvider : ILocalizedAssetProvider, ITableScopedLocalizedAssetProvider
    {
        private readonly List<string> _collections = new List<string>();
        private readonly HashSet<string> _collectionSet = new HashSet<string>();
        private readonly HashSet<string> _textCollections = new HashSet<string>();
        private readonly Dictionary<string, string> _keyToCollection = new Dictionary<string, string>();
        private readonly HashSet<string> _unknownTablesReported = new HashSet<string>();
        private readonly IAssetTableReader _reader;

        /// <summary>Searches <paramref name="collectionNames"/> (the manifest's asset collections).</summary>
        public UnityLocalizedAssetProvider(IEnumerable<string> collectionNames)
            : this(collectionNames, null, new UnityAssetTableReader()) { }

        /// <summary>
        /// As above, also knowing the manifest's text collections — which lets a targeted lookup tell a known table
        /// without asset collections (null, nothing opened) from a table the build never produced (classic lookup).
        /// </summary>
        public UnityLocalizedAssetProvider(IEnumerable<string> collectionNames, IEnumerable<string> textCollectionNames)
            : this(collectionNames, textCollectionNames, new UnityAssetTableReader()) { }

        internal UnityLocalizedAssetProvider(IEnumerable<string> collectionNames, IEnumerable<string> textCollectionNames,
            IAssetTableReader reader)
        {
            _reader = reader;
            if (collectionNames != null)
                foreach (var c in collectionNames)
                    if (!string.IsNullOrEmpty(c) && _collectionSet.Add(c)) _collections.Add(c);
            if (textCollectionNames != null)
                foreach (var c in textCollectionNames)
                    if (!string.IsNullOrEmpty(c)) _textCollections.Add(c);
        }

        /// <summary>How many distinct tables were looked up while absent from the manifest (each warned once).</summary>
        internal int UnknownTablesReported => _unknownTablesReported.Count;

        public T ResolveAsset<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (_keyToCollection.TryGetValue(key, out var cached) && _reader.TryRead<T>(cached, key, out var cachedAsset))
                return cachedAsset;

            foreach (var collection in _collections)
            {
                if (!_reader.TryRead<T>(collection, key, out var asset)) continue;
                _keyToCollection[key] = collection;
                return asset;
            }
            return null;
        }

        /// <summary>
        /// Resolves the asset for <paramref name="key"/> reading only <paramref name="table"/>'s asset collections.
        /// An empty table is the classic <see cref="ResolveAsset{T}"/>.
        /// </summary>
        public T ResolveAssetInTable<T>(string table, string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (string.IsNullOrEmpty(table)) return ResolveAsset<T>(key);

            bool anyCandidate = false;
            foreach (var (_, typeName) in LocalizationTableNames.AssetTypes)
            {
                var collection = LocalizationTableNames.AssetCollection(table, typeName);
                if (!_collectionSet.Contains(collection)) continue;
                anyCandidate = true;
                if (_reader.TryRead<T>(collection, key, out var asset)) return asset;
            }
            if (anyCandidate) return null;

            // No asset collection for this table: a known table simply has no localized assets. Only a table the
            // manifest has never heard of (renamed/cloned graph, stale build) falls back to the classic lookup.
            if (_textCollections.Count == 0 || _textCollections.Contains(LocalizationTableNames.TextCollection(table)))
                return null;

            if (_unknownTablesReported.Add(table))
                Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback",
                    $"[GraphLocalization] Table '{table}' is not in the localization manifest (renamed or cloned graph, " +
                    "or tables not rebuilt). Searching every asset table instead — run Faolline ▸ Localization ▸ Build " +
                    "All Tables, and look graphs up by their asset name.");
            return ResolveAsset<T>(key);
        }
    }
}
#endif
