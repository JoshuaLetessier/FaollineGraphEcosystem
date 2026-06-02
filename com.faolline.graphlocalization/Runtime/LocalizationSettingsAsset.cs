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
        public LocaleValidationMode LocaleValidation => _localeValidation;
        public LocalizationStrictMode PlayerStrictMode => _playerStrictMode;

        /// <summary>Locale codes generated as CSV columns (Csv mode). First is the source locale.</summary>
        public IReadOnlyList<string> CsvLocales => _csvLocales;

        /// <summary>Folder where per-lib CSV files are written (Csv mode).</summary>
        public string CsvOutputFolder => string.IsNullOrEmpty(_csvOutputFolder) ? "Assets/Localization/Csv" : _csvOutputFolder;

        public LocalizationSettings CreateSettings(string locale = "en")
        {
            var provider = CreateProviderForMode();
            return new LocalizationSettings(provider, locale) { StrictMode = _playerStrictMode };
        }

        private ILocalizationProvider CreateProviderForMode()
        {
#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
            if (_mode == LocalizationMode.UnityLocalization)
                return new Unity.UnityLocalizationProvider(_unityLocalizationTableName);
#endif
            return new CsvLocalizationProvider("Key,en\n", "en");
        }
    }
}
