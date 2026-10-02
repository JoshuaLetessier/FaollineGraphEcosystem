using System.Collections.Generic;
using Faolline.GraphLocalization;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// Test double for a table-aware localization backend: answers only from (table, key) pairs it was given,
    /// and records every scoped and classic call — so a test can assert WHICH table each text was looked up in.
    /// </summary>
    internal sealed class ScopedRecordingProvider : ILocalizationProvider, ITableScopedLocalizationProvider
    {
        private readonly Dictionary<(string table, string key), string> _values = new Dictionary<(string, string), string>();
        public readonly List<(string table, string key)> ScopedCalls = new List<(string, string)>();
        public readonly List<string> ClassicCalls = new List<string>();

        public string CurrentLocale { get; private set; } = "en";
        public void SetLocale(string locale) { if (!string.IsNullOrEmpty(locale)) CurrentLocale = locale; }

        public ScopedRecordingProvider With(string table, string key, string value)
        {
            _values[(table, key)] = value;
            return this;
        }

        public string Resolve(string key, string locale)
        {
            ClassicCalls.Add(key);
            foreach (var kv in _values) if (kv.Key.key == key) return kv.Value;
            return "#" + key;
        }

        public string ResolveInTable(string table, string key, string locale)
        {
            ScopedCalls.Add((table, key));
            return _values.TryGetValue((table, key), out var v) ? v : "#" + key;
        }

        /// <summary>Distinct tables asked through the scoped call.</summary>
        public HashSet<string> TablesAsked()
        {
            var set = new HashSet<string>();
            foreach (var (table, _) in ScopedCalls) set.Add(table);
            return set;
        }
    }

    /// <summary>Table-aware localized-asset double recording which table each asset was looked up in.</summary>
    internal sealed class ScopedRecordingAssets : ILocalizedAssetProvider, ITableScopedLocalizedAssetProvider
    {
        public readonly List<(string table, string key)> ScopedCalls = new List<(string, string)>();
        public readonly List<string> ClassicCalls = new List<string>();

        public T ResolveAsset<T>(string key) where T : Object { ClassicCalls.Add(key); return null; }
        public T ResolveAssetInTable<T>(string table, string key) where T : Object { ScopedCalls.Add((table, key)); return null; }
    }
}
