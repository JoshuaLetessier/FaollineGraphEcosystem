namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Ambient accessor for the current <see cref="LocalizationSettings"/>.
    /// Prefer explicit injection where possible; use this for code paths that cannot receive a provider.
    /// </summary>
    public static class LocalizationContext
    {
        private static LocalizationSettings _current;

        public static LocalizationSettings Current
        {
            get
            {
                if (_current == null)
                {
                    var asset = LocalizationSettingsLoader.Load();
                    _current = asset != null ? asset.CreateSettings() : new LocalizationSettings();
                }
                return _current;
            }
            set => _current = value;
        }

        public static string Resolve(string key) => Current.Resolve(key);
    }
}
