#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// <see cref="ILocalizedAssetProvider"/> backed by Unity Localization **Asset Tables**. Resolves an
    /// asset by the same key as the text, for the selected locale, by searching the asset-table collections
    /// recorded in the build manifest (caching key → collection). Returns null when the key is unknown or
    /// the asset is unassigned for the active locale. Gated so projects without com.unity.localization take
    /// no dependency.
    /// </summary>
    public sealed class UnityLocalizedAssetProvider : ILocalizedAssetProvider
    {
        private readonly List<string> _collections = new List<string>();
        private readonly Dictionary<string, string> _keyToCollection = new Dictionary<string, string>();

        public UnityLocalizedAssetProvider(IEnumerable<string> collectionNames)
        {
            if (collectionNames != null)
                foreach (var c in collectionNames)
                    if (!string.IsNullOrEmpty(c) && !_collections.Contains(c)) _collections.Add(c);
        }

        public T ResolveAsset<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (UnityLocalizationSettings.AssetDatabase == null) return null;

            if (_keyToCollection.TryGetValue(key, out var cached) && TryResolve<T>(cached, key, out var cachedAsset))
                return cachedAsset;

            foreach (var collection in _collections)
            {
                if (!TryResolve<T>(collection, key, out var asset)) continue;
                _keyToCollection[key] = collection;
                return asset;
            }
            return null;
        }

        /// <summary>True when <paramref name="collection"/> defines <paramref name="key"/>; out asset may be
        /// null when the key exists but is unassigned for the active locale.</summary>
        private static bool TryResolve<T>(string collection, string key, out T asset) where T : Object
        {
            asset = null;
            if (string.IsNullOrEmpty(collection)) return false;
            try
            {
                var table = UnityLocalizationSettings.AssetDatabase.GetTableAsync(collection).WaitForCompletion() as AssetTable;
                var shared = table != null ? table.SharedData : null;
                if (shared == null) return false;
                if (shared.GetEntry(key) == null) return false; // key not defined in this collection

                asset = UnityLocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<T>(collection, key).WaitForCompletion();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
