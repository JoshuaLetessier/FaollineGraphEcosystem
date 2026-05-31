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

        /// <summary>The current settings. Never null — a safe default is created on first access.</summary>
        public static LocalizationSettings Current
        {
            get => _current ??= new LocalizationSettings();
            set => _current = value;
        }

        /// <summary>Resolves <paramref name="key"/> through <see cref="Current"/>.</summary>
        public static string Resolve(string key) => Current.Resolve(key);
    }
}
