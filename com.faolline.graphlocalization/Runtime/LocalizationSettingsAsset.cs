using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Project-wide localization configuration asset. One per project, stored in Resources.
    /// </summary>
    public class LocalizationSettingsAsset : ScriptableObject
    {
        [Header("Provider")]
        [SerializeField, Tooltip("Project-wide localization backend.\n\n" +
            "• Csv: lightweight in-memory table, no external package.\n" +
            "• UnityLocalization: com.unity.localization String Tables (if installed).")]
        private LocalizationMode _mode = LocalizationMode.Csv;

        [SerializeField, Tooltip("Unity Localization String Table collection name used when Mode = UnityLocalization.")]
        private string _unityLocalizationTableName = "Dialogue";

        [SerializeField, Tooltip("UnityLocalization mode: which tables Build All Tables generates.\n\n" +
            "• Text: String Tables only (classic).\n" +
            "• Asset: Asset Tables only (localized audio).\n" +
            "• Both: String + mirror Asset Tables.")]
        private UnityTableMode _unityTableMode = UnityTableMode.Text;

        [Header("CSV mode")]
        [SerializeField, Tooltip("Locale codes to generate as columns in the CSV files (Csv mode). " +
            "The first locale is the source — its column is pre-filled with the node/choice/speaker " +
            "default text. Others start empty for translators.")]
        private List<string> _csvLocales = new List<string> { "en" };

        [SerializeField, Tooltip("Folder where per-lib CSV files are written (Csv mode).")]
        private string _csvOutputFolder = "Assets/Localization/Csv";

        [Header("Build-time validation")]
        [SerializeField, Tooltip("How the table builder reports per-locale translation gaps.\n\n" +
            "• Permissive: accept gaps silently.\n" +
            "• Warn (default): log warnings, never block.\n" +
            "• Strict: log gaps as errors (pre-release QA gate).")]
        private LocaleValidationMode _localeValidation = LocaleValidationMode.Warn;

        [Header("Runtime playback")]
        [SerializeField, Tooltip("How the player reacts when a key cannot be resolved during playback.\n\n" +
            "• Permissive: use the #key fallback silently.\n" +
            "• Audit (default): use the fallback, warn, and record the key for a report.\n" +
            "• Strict: throw a LocalizationException on the first missing key.")]
        private LocalizationStrictMode _playerStrictMode = LocalizationStrictMode.Audit;

        public LocalizationMode Mode => _mode;
        public string UnityLocalizationTableName => _unityLocalizationTableName;

        /// <summary>UnityLocalization mode: which tables the build generates.</summary>
        public UnityTableMode UnityTableMode => _unityTableMode;

        /// <summary>True when the build should create/sync String Tables (Text or Both).</summary>
        public bool GeneratesStringTables => _unityTableMode != UnityTableMode.Asset;

        /// <summary>True when the build should create/sync Asset Tables (Asset or Both).</summary>
        public bool GeneratesAssetTables => _unityTableMode != UnityTableMode.Text;
        public LocaleValidationMode LocaleValidation => _localeValidation;
        public LocalizationStrictMode PlayerStrictMode => _playerStrictMode;

        /// <summary>Locale codes generated as CSV columns (Csv mode). First is the source locale.</summary>
        public IReadOnlyList<string> CsvLocales => _csvLocales;

        /// <summary>Folder where per-lib CSV files are written (Csv mode).</summary>
        public string CsvOutputFolder => string.IsNullOrEmpty(_csvOutputFolder) ? "Assets/Localization/Csv" : _csvOutputFolder;

        public LocalizationSettings CreateSettings(string locale = "en")
        {
            var provider = CreateProviderForMode();
            return new LocalizationSettings(provider, locale)
            {
                StrictMode = _playerStrictMode,
                AssetProvider = CreateAssetProviderForMode()
            };
        }

        /// <summary>
        /// Builds the localized-asset provider for UnityLocalization mode (via reflection, like the string
        /// provider). Returns null in CSV mode or when no asset collections were produced.
        /// </summary>
        private ILocalizedAssetProvider CreateAssetProviderForMode()
        {
            if (_mode != LocalizationMode.UnityLocalization) return null;

            var manifest = GraphLocalizationManifest.Load();
            var collections = manifest != null ? manifest.AllUnityAssetCollections() : null;
            if (collections == null || collections.Count == 0) return null;

            var type = Type.GetType(
                "Faolline.GraphLocalization.Unity.UnityLocalizedAssetProvider, " +
                "com.faolline.graphlocalization.Localization.Unity");
            if (type == null) return null;

            var ctor = type.GetConstructor(new[] { typeof(IEnumerable<string>) });
            if (ctor == null) return null;

            return ctor.Invoke(new object[] { collections }) as ILocalizedAssetProvider;
        }

        private ILocalizationProvider CreateProviderForMode()
        {
            var manifest = GraphLocalizationManifest.Load();

            if (_mode == LocalizationMode.UnityLocalization)
            {
                // The Unity provider lives in a gated assembly that references this Runtime, so we cannot
                // reference it back (circular). Construct it via reflection instead — the same seam the
                // builder uses for the syncer. Falls back to CSV if com.unity.localization is absent.
                var collections = manifest != null ? manifest.AllUnityCollections() : new List<string>();
                var unityProvider = TryCreateUnityProvider(collections, _unityLocalizationTableName);
                if (unityProvider != null) return unityProvider;
                Debug.LogWarning("[GraphLocalization] Mode is UnityLocalization but the Unity provider could not " +
                    "be created (is com.unity.localization installed?). Falling back to CSV.");
            }

            // CSV mode (or fallback): merge every generated CSV so keys spread across per-graph files resolve.
            var provider = new CsvLocalizationProvider("Key,en\n", "en");
            if (manifest != null)
                foreach (var csv in manifest.AllCsvFiles())
                    if (csv != null) provider.Append(csv.text);
            return provider;
        }

        private static ILocalizationProvider TryCreateUnityProvider(IEnumerable<string> collections, string fallbackCollectionName)
        {
            var type = Type.GetType(
                "Faolline.GraphLocalization.Unity.UnityLocalizationProvider, " +
                "com.faolline.graphlocalization.Localization.Unity");
            if (type == null) return null;

            var ctor = type.GetConstructor(new[] { typeof(IEnumerable<string>), typeof(string) });
            if (ctor == null) return null;

            return ctor.Invoke(new object[] { collections, fallbackCollectionName }) as ILocalizationProvider;
        }
    }
}
