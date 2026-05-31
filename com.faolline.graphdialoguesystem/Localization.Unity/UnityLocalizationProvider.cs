#if GRAPHDIALOGUE_UNITY_LOCALIZATION
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphDialogue.Localization.Unity
{
    /// <summary>
    /// Optional <see cref="ILocalizationProvider"/> backed by Unity's <c>com.unity.localization</c>
    /// String Tables. Lives in a separate, gated assembly so projects that do not use Unity
    /// Localization take no dependency on it (Constitution v1.2.0). Resolves a key against a single
    /// String Table collection; on a missing entry returns the defined <c>#key</c> fallback.
    /// </summary>
    public sealed class UnityLocalizationProvider : ILocalizationProvider
    {
        private readonly string _tableCollectionName;

        public UnityLocalizationProvider(string tableCollectionName)
        {
            _tableCollectionName = tableCollectionName;
        }

        /// <inheritdoc/>
        public string CurrentLocale
        {
            get
            {
                var locale = UnityLocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : "en";
            }
        }

        /// <inheritdoc/>
        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var db = UnityLocalizationSettings.StringDatabase;
            if (db == null) return $"#{key}";

            // GetLocalizedString resolves against the currently selected locale; callers switch the
            // active locale through Unity's LocalizationSettings.SelectedLocale.
            var value = db.GetLocalizedString(_tableCollectionName, key);
            return string.IsNullOrEmpty(value) ? $"#{key}" : value;
        }
    }
}
#endif
