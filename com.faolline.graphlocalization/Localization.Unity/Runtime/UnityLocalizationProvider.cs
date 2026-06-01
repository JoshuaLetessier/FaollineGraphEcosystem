#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using UnityEngine.Localization.Settings;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// <see cref="ILocalizationProvider"/> backed by Unity's com.unity.localization String Tables.
    /// Resolves a key against a named collection; returns the #key fallback on miss.
    /// Lives in a gated assembly so projects without com.unity.localization take no dependency.
    /// </summary>
    public sealed class UnityLocalizationProvider : ILocalizationProvider
    {
        private readonly string _tableCollectionName;

        public UnityLocalizationProvider(string tableCollectionName)
            => _tableCollectionName = tableCollectionName;

        public string CurrentLocale
        {
            get
            {
                var locale = UnityLocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : "en";
            }
        }

        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var db = UnityLocalizationSettings.StringDatabase;
            if (db == null) return $"#{key}";
            var value = db.GetLocalizedString(_tableCollectionName, key);
            return string.IsNullOrEmpty(value) ? $"#{key}" : value;
        }
    }
}
#endif
