#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using System.Collections.Generic;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// <see cref="ILocalizationProvider"/> backed by Unity's com.unity.localization String Tables.
    /// Keys are spread across per-graph and per-group collections (good for translators, and for packaging
    /// tables separately, e.g. one Addressables group per chapter).
    /// <para>
    /// <b>Targeted lookup</b> (<see cref="ResolveInTable"/>): a caller that knows the key's table — a dialogue's
    /// graph, a speaker's group, a quest's graph — reads ONLY that collection, so unrelated tables are never
    /// loaded. A table absent from the build manifest (e.g. a graph renamed "X(Clone)" by Instantiate) falls back
    /// to the classic lookup, reported once per table.
    /// </para>
    /// <para>
    /// <b>Classic lookup</b> (<see cref="Resolve"/>): searches the manifest's collections in order and caches which
    /// collection holds each key.
    /// </para>
    /// Both return the #key fallback when no collection contains the key. Lives in a gated assembly so projects
    /// without com.unity.localization take no dependency.
    /// </summary>
    public sealed class UnityLocalizationProvider : ILocalizationProvider, ITableScopedLocalizationProvider
    {
        private readonly List<string> _collections = new List<string>();
        private readonly HashSet<string> _collectionSet = new HashSet<string>();
        private readonly Dictionary<string, string> _keyToCollection = new Dictionary<string, string>();
        private readonly HashSet<string> _unknownTablesReported = new HashSet<string>();
        private readonly IStringTableReader _reader;

        /// <summary>
        /// Searches <paramref name="collectionNames"/> (typically every collection in the build manifest).
        /// <paramref name="fallbackCollectionName"/> is used only when the list is empty (back-compat).
        /// </summary>
        public UnityLocalizationProvider(IEnumerable<string> collectionNames, string fallbackCollectionName = null)
            : this(collectionNames, fallbackCollectionName, new UnityStringTableReader()) { }

        /// <summary>Back-compat single-collection constructor.</summary>
        public UnityLocalizationProvider(string tableCollectionName) : this(null, tableCollectionName) { }

        internal UnityLocalizationProvider(IEnumerable<string> collectionNames, string fallbackCollectionName, IStringTableReader reader)
        {
            _reader = reader;
            if (collectionNames != null)
                foreach (var c in collectionNames)
                    if (!string.IsNullOrEmpty(c) && _collectionSet.Add(c)) _collections.Add(c);
            if (_collections.Count == 0 && !string.IsNullOrEmpty(fallbackCollectionName) && _collectionSet.Add(fallbackCollectionName))
                _collections.Add(fallbackCollectionName);
        }

        /// <summary>How many distinct tables were looked up while absent from the manifest (each warned once).</summary>
        internal int UnknownTablesReported => _unknownTablesReported.Count;

        // Unity Localization loads its locales asynchronously: before initialization completes,
        // AvailableLocales is empty and SelectedLocale null — an early SetLocale used to no-op SILENTLY
        // (worst in a player build, where a locale set from a boot script simply never applied). We block
        // once on the initialization handle (the documented synchronous pattern) before touching locales.
        private bool _initAttempted;

        private void EnsureInitialized()
        {
            if (_initAttempted) return;
            _initAttempted = true;
            try { UnityLocalizationSettings.InitializationOperation.WaitForCompletion(); }
            catch (System.Exception e)
            {
                Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback",
                    $"[GraphLocalization] Unity Localization failed to initialize: {e.Message}. " +
                    "Locale queries/changes may not apply.");
            }
        }

        public string CurrentLocale
        {
            get
            {
                EnsureInitialized();
                var locale = UnityLocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : "en";
            }
        }

        public void SetLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return;
            EnsureInitialized();
            var available = UnityLocalizationSettings.AvailableLocales;
            if (available == null || available.Locales == null || available.Locales.Count == 0)
            {
                Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback",
                    $"[GraphLocalization] SetLocale('{locale}') ignored: Unity Localization has no available " +
                    "locales (initialization failed, or no locales are configured in Project Settings ▸ " +
                    "Localization).");
                return;
            }
            var target = available.GetLocale(new UnityEngine.Localization.LocaleIdentifier(locale));
            if (target != null)
            {
                UnityLocalizationSettings.SelectedLocale = target;
                return;
            }
            var codes = new List<string>();
            foreach (var l in available.Locales)
                if (l != null) codes.Add(l.Identifier.Code);
            Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback",
                $"[GraphLocalization] SetLocale('{locale}') ignored: no such locale among the project's " +
                $"({string.Join(", ", codes)}).");
        }

        private bool _warnedNoCollections;

        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_collections.Count == 0 && !_warnedNoCollections)
            {
                _warnedNoCollections = true;
                Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback", "[GraphLocalization] UnityLocalizationProvider has no collections to " +
                    "search. Run Faolline ▸ Localization ▸ Build All Tables to (re)generate the manifest.");
            }

            // Fast path: a collection already known to hold this key.
            if (_keyToCollection.TryGetValue(key, out var cached) && _reader.TryRead(cached, key, out var cachedValue))
                return string.IsNullOrEmpty(cachedValue) ? $"#{key}" : cachedValue;

            foreach (var collection in _collections)
            {
                if (!_reader.TryRead(collection, key, out var value)) continue;
                _keyToCollection[key] = collection;
                return string.IsNullOrEmpty(value) ? $"#{key}" : value;
            }
            return $"#{key}";
        }

        /// <summary>
        /// Resolves <paramref name="key"/> reading only the collection of <paramref name="table"/>
        /// (<see cref="LocalizationTableNames.TextCollection"/>). A key missing from that table is the #key marker —
        /// no other collection is opened. An empty table is the classic <see cref="Resolve"/>; a table absent from
        /// the manifest falls back to it too, reported once per table.
        /// </summary>
        public string ResolveInTable(string table, string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (string.IsNullOrEmpty(table)) return Resolve(key, locale);

            var collection = LocalizationTableNames.TextCollection(table);
            if (!_collectionSet.Contains(collection))
            {
                if (_unknownTablesReported.Add(table))
                    Faolline.GraphLogging.Logging.Warning("GraphLocalization.Playback",
                        $"[GraphLocalization] Table '{table}' is not in the localization manifest (renamed or cloned graph, " +
                        "or tables not rebuilt). Searching every table instead — run Faolline ▸ Localization ▸ Build All " +
                        "Tables, and look graphs up by their asset name.");
                return Resolve(key, locale);
            }

            return _reader.TryRead(collection, key, out var value) && !string.IsNullOrEmpty(value) ? value : $"#{key}";
        }
    }
}
#endif
