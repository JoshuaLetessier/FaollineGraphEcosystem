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

        public string Resolve(string key) => Provider.Resolve(key, _currentLocale);
    }
}
