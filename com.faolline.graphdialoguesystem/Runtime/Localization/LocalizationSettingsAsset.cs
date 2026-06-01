using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Serializable asset that stores project-wide localization configuration.
    /// Wrap in LocalizationSettings for runtime use.
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
        [SerializeField, Tooltip("How the table builder reports per-locale translation gaps (a key with an empty value in some locale).\n\n" +
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

        /// <summary>How per-locale translation gaps are reported when building tables. Default: Warn.</summary>
        public LocaleValidationMode LocaleValidation => _localeValidation;

        /// <summary>How the runtime player reacts to a missing key during playback. Default: Audit.</summary>
        public LocalizationStrictMode PlayerStrictMode => _playerStrictMode;

        /// <summary>Create a LocalizationSettings instance from this asset configuration.</summary>
        public LocalizationSettings CreateSettings(string locale = "en")
        {
            ILocalizationProvider provider = CreateProviderForMode();
            return new LocalizationSettings(provider, locale) { StrictMode = _playerStrictMode };
        }

        private ILocalizationProvider CreateProviderForMode()
        {
#if GRAPHDIALOGUE_UNITY_LOCALIZATION
            if (_mode == LocalizationMode.UnityLocalization)
                return new Localization.Unity.UnityLocalizationProvider(_unityLocalizationTableName);
#endif
            return new CsvLocalizationProvider("Key,en\n", "en");
        }
    }
}
