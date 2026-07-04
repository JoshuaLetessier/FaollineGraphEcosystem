namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Runtime selection of the active <see cref="ILocalizationProvider"/> and current locale.
    /// Created from a <see cref="LocalizationSettingsAsset"/> or programmatically.
    /// </summary>
    public sealed class LocalizationSettings
    {
        private ILocalizationProvider _provider;
        private string _currentLocale = "en";

        /// <summary>How the runtime player reacts to a missing key during playback. Default: Audit.</summary>
        public LocalizationStrictMode StrictMode { get; set; } = LocalizationStrictMode.Audit;

        public LocalizationSettings(ILocalizationProvider provider = null, string locale = "en")
        {
            _provider = provider;
            _currentLocale = string.IsNullOrEmpty(locale) ? "en" : locale;
        }

        public ILocalizationProvider Provider
        {
            get => _provider ??= new CsvLocalizationProvider(string.Empty, _currentLocale);
            set => _provider = value;
        }

        /// <summary>
        /// Optional provider for localized assets (e.g. voice clips) resolved by the same key as the text.
        /// Null when no localized-asset backend is configured (the default).
        /// </summary>
        public ILocalizedAssetProvider AssetProvider { get; set; }

        public string CurrentLocale
        {
            get => _currentLocale;
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                _currentLocale = value;
                _provider?.SetLocale(value);
            }
        }

        private System.Collections.Generic.HashSet<string> _auditedMissing;

        /// <summary>
        /// Resolves <paramref name="key"/> in the current locale, applying <see cref="StrictMode"/> when the
        /// key is missing: Permissive returns the <c>#key</c> marker silently; Audit warns once per key+locale
        /// and returns the marker; Strict throws a <see cref="LocalizationException"/>. (Providers themselves
        /// never log — the marker is the signal; this is the layer that decides how loudly to react.)
        /// </summary>
        public string Resolve(string key)
        {
            var value = Provider.Resolve(key, _currentLocale);
            if (string.IsNullOrEmpty(key) || value != $"#{key}") return value;

            switch (StrictMode)
            {
                case LocalizationStrictMode.Strict:
                    throw new LocalizationException(key, _currentLocale);
                case LocalizationStrictMode.Audit:
                    var stamp = $"{_currentLocale} {key}";
                    if ((_auditedMissing ??= new System.Collections.Generic.HashSet<string>()).Add(stamp))
                        UnityEngine.Debug.LogWarning(
                            $"[GraphLocalization] Missing localization key '{key}' for locale '{_currentLocale}'.");
                    break;
                // Permissive: return the marker silently.
            }
            return value;
        }
    }
}
