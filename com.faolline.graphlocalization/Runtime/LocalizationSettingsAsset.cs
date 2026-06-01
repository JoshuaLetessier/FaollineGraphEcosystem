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
