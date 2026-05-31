namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Lightweight selection of the active <see cref="ILocalizationProvider"/> and current locale.
    /// When no provider is configured, a safe default (an empty <see cref="CsvLocalizationProvider"/>)
    /// is used so resolution never fails for lack of setup — missing keys return the defined fallback.
    /// </summary>
    public sealed class LocalizationSettings
    {
        private ILocalizationProvider _provider;
        private string _currentLocale = "en";

        /// <summary>
        /// The active provider. Setting it to null falls back to a safe default provider on next read.
        /// </summary>
        public ILocalizationProvider Provider
        {
            get => _provider ??= new CsvLocalizationProvider(string.Empty, _currentLocale);
            set => _provider = value;
        }

        /// <summary>The current locale code (e.g. "en", "fr"). Never null/empty.</summary>
        public string CurrentLocale
        {
            get => _currentLocale;
            set { if (!string.IsNullOrEmpty(value)) _currentLocale = value; }
        }

        /// <summary>Resolves <paramref name="key"/> via the active provider and current locale.</summary>
        public string Resolve(string key) => Provider.Resolve(key, _currentLocale);
    }
}
