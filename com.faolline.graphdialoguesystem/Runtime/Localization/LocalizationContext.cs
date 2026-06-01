namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Ambient accessor for the current <see cref="LocalizationSettings"/>. Optional convenience for
    /// code paths that cannot receive an injected provider; always returns a usable, safe-default
    /// instance. Prefer explicit injection (e.g. into <see cref="DialoguePlayer"/>) where possible.
    /// </summary>
    public static class LocalizationContext
    {
        private static LocalizationSettings _current;

        /// <summary>The current settings. Never null — loads from asset or creates safe default on first access.</summary>
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

        /// <summary>Resolves <paramref name="key"/> through <see cref="Current"/>.</summary>
        public static string Resolve(string key) => Current.Resolve(key);
    }
}
