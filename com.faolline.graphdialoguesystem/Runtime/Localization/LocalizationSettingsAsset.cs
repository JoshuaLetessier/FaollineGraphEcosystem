using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Serializable asset that stores project-wide localization configuration.
    /// Wrap in LocalizationSettings for runtime use.
    /// </summary>
    public class LocalizationSettingsAsset : ScriptableObject
    {
        [SerializeField] private LocalizationMode _mode = LocalizationMode.Csv;
        [SerializeField] private string _unityLocalizationTableName = "Dialogue";

        public LocalizationMode Mode => _mode;
        public string UnityLocalizationTableName => _unityLocalizationTableName;

        /// <summary>Create a LocalizationSettings instance from this asset configuration.</summary>
        public LocalizationSettings CreateSettings(string locale = "en")
        {
            ILocalizationProvider provider = CreateProviderForMode();
            return new LocalizationSettings(provider, locale);
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
