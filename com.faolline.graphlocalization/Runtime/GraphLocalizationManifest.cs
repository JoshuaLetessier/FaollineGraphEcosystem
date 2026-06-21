using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Build-time index of the localization artifacts produced for each graph lib, persisted in
    /// <c>Resources</c> so the runtime providers can discover them. Keys are spread across per-graph
    /// collections/files (good for translators); this manifest lets a provider find which
    /// collection/file holds a given key without runtime asset enumeration.
    /// Written by the localization builder; read by <see cref="LocalizationSettingsAsset"/> when it
    /// creates a provider.
    /// </summary>
    public sealed class GraphLocalizationManifest : ScriptableObject
    {
        /// <summary>Resources name (no extension). Asset lives at Assets/Resources/GraphLocalizationManifest.asset.</summary>
        public const string ResourceName = "GraphLocalizationManifest";

        [System.Serializable]
        public sealed class LibEntry
        {
            public string LibName;
            /// <summary>Unity Localization String Table collection names produced for this lib.</summary>
            public List<string> UnityCollections = new List<string>();
            /// <summary>Unity Localization Asset Table collection names produced for this lib (localized audio, etc.).</summary>
            public List<string> UnityAssetCollections = new List<string>();
            /// <summary>CSV files produced for this lib (asset references; loadable at runtime).</summary>
            public List<TextAsset> CsvFiles = new List<TextAsset>();
            public string LastBuildTime;
            public int TotalGraphsScanned;
            public int TotalKeysFound;
        }

        [SerializeField] private List<LibEntry> _libs = new List<LibEntry>();

        public IReadOnlyList<LibEntry> Libs => _libs;

        /// <summary>Loads the manifest from Resources, or null if no build has produced one yet.</summary>
        public static GraphLocalizationManifest Load() => Resources.Load<GraphLocalizationManifest>(ResourceName);

        /// <summary>Gets or creates the entry for a lib (editor/build use).</summary>
        public LibEntry GetOrCreateLib(string libName)
        {
            var e = _libs.Find(x => x.LibName == libName);
            if (e == null) { e = new LibEntry { LibName = libName }; _libs.Add(e); }
            return e;
        }

        /// <summary>All Unity collection names across every lib, de-duplicated.</summary>
        public List<string> AllUnityCollections()
        {
            var result = new List<string>();
            foreach (var lib in _libs)
                foreach (var c in lib.UnityCollections)
                    if (!string.IsNullOrEmpty(c) && !result.Contains(c)) result.Add(c);
            return result;
        }

        /// <summary>All Unity Asset Table collection names across every lib, de-duplicated.</summary>
        public List<string> AllUnityAssetCollections()
        {
            var result = new List<string>();
            foreach (var lib in _libs)
                foreach (var c in lib.UnityAssetCollections)
                    if (!string.IsNullOrEmpty(c) && !result.Contains(c)) result.Add(c);
            return result;
        }

        /// <summary>All CSV files across every lib, de-duplicated.</summary>
        public List<TextAsset> AllCsvFiles()
        {
            var result = new List<TextAsset>();
            foreach (var lib in _libs)
                foreach (var f in lib.CsvFiles)
                    if (f != null && !result.Contains(f)) result.Add(f);
            return result;
        }
    }
}
