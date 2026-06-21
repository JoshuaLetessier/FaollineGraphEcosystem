#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using System.Collections.Generic;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// <see cref="ILocalizationProvider"/> backed by Unity's com.unity.localization String Tables.
    /// Keys are spread across per-graph collections (good for translators), so this provider searches
    /// the set of collections produced for the project (from the build manifest) and caches which
    /// collection holds each key. Returns the #key fallback when no collection contains the key.
    /// Lives in a gated assembly so projects without com.unity.localization take no dependency.
    /// </summary>
    public sealed class UnityLocalizationProvider : ILocalizationProvider
    {
        private readonly List<string> _collections = new List<string>();
        private readonly Dictionary<string, string> _keyToCollection = new Dictionary<string, string>();

        /// <summary>
        /// Searches <paramref name="collectionNames"/> (typically every collection in the build manifest).
        /// <paramref name="fallbackCollectionName"/> is used only when the list is empty (back-compat).
        /// </summary>
        public UnityLocalizationProvider(IEnumerable<string> collectionNames, string fallbackCollectionName = null)
        {
            if (collectionNames != null)
                foreach (var c in collectionNames)
                    if (!string.IsNullOrEmpty(c) && !_collections.Contains(c)) _collections.Add(c);
            if (_collections.Count == 0 && !string.IsNullOrEmpty(fallbackCollectionName))
                _collections.Add(fallbackCollectionName);
        }

        /// <summary>Back-compat single-collection constructor.</summary>
        public UnityLocalizationProvider(string tableCollectionName) : this(null, tableCollectionName) { }

        public string CurrentLocale
        {
            get
            {
                var locale = UnityLocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : "en";
            }
        }

        public void SetLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return;
            var available = UnityLocalizationSettings.AvailableLocales;
            if (available == null) return;
            var target = available.GetLocale(new UnityEngine.Localization.LocaleIdentifier(locale));
            if (target != null) UnityLocalizationSettings.SelectedLocale = target;
        }

        private bool _warnedNoCollections;

        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (UnityLocalizationSettings.StringDatabase == null) return $"#{key}";

            if (_collections.Count == 0 && !_warnedNoCollections)
            {
                _warnedNoCollections = true;
                UnityEngine.Debug.LogWarning("[GraphLocalization] UnityLocalizationProvider has no collections to " +
                    "search. Run Faolline ▸ Localization ▸ Build All Tables to (re)generate the manifest.");
            }

            // Fast path: a collection already known to hold this key.
            if (_keyToCollection.TryGetValue(key, out var cached) && TryResolveIn(cached, key, out var cachedValue))
                return string.IsNullOrEmpty(cachedValue) ? $"#{key}" : cachedValue;

            foreach (var collection in _collections)
            {
                if (!TryResolveIn(collection, key, out var value)) continue;
                _keyToCollection[key] = collection;
                return string.IsNullOrEmpty(value) ? $"#{key}" : value;
            }
            return $"#{key}";
        }

        /// <summary>
        /// True when <paramref name="collection"/> defines <paramref name="key"/> (regardless of whether the
        /// current locale has a translation). <paramref name="value"/> is the selected-locale value, or — when
        /// that is empty — the first non-empty value from any other locale (graceful fallback to the source
        /// text), or empty when the key exists but is untranslated everywhere.
        /// </summary>
        private static bool TryResolveIn(string collection, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(collection)) return false;
            try
            {
                var db = UnityLocalizationSettings.StringDatabase;
                var table = db.GetTableAsync(collection).WaitForCompletion();
                var shared = table != null ? table.SharedData : null;
                if (shared == null) return false;

                var sharedEntry = shared.GetEntry(key);
                if (sharedEntry == null) return false; // key not defined in this collection

                // Selected locale first.
                var selected = table.GetEntry(sharedEntry.Id);
                var selectedValue = selected != null ? selected.GetLocalizedString() : null;
                if (!string.IsNullOrEmpty(selectedValue)) { value = selectedValue; return true; }

                // Graceful fallback: any locale with a non-empty value (typically the source text).
                var locales = UnityLocalizationSettings.AvailableLocales != null
                    ? UnityLocalizationSettings.AvailableLocales.Locales : null;
                if (locales != null)
                {
                    foreach (var loc in locales)
                    {
                        if (loc == null) continue;
                        var localeTable = db.GetTableAsync(collection, loc).WaitForCompletion();
                        var entry = localeTable != null ? localeTable.GetEntry(sharedEntry.Id) : null;
                        var localeValue = entry != null ? entry.GetLocalizedString() : null;
                        if (!string.IsNullOrEmpty(localeValue)) { value = localeValue; return true; }
                    }
                }

                value = string.Empty; // key exists but untranslated everywhere
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
