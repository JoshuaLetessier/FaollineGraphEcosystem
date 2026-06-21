namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Neutral text-resolution contract the runtime depends on, independent of any specific
    /// localization technology. Implementations resolve a text key into a displayed string for a locale,
    /// and MUST return a defined, non-empty fallback (never null/empty) when a key is absent.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>The currently active locale code (e.g. "en", "fr"). Never null/empty.</summary>
        string CurrentLocale { get; }

        /// <summary>
        /// Changes the active locale. Subsequent <see cref="Resolve"/> calls using
        /// <see cref="CurrentLocale"/> will return text in the new locale. Ignored if
        /// <paramref name="locale"/> is null or empty.
        /// </summary>
        void SetLocale(string locale);

        /// <summary>
        /// Resolves <paramref name="key"/> in <paramref name="locale"/>. Returns the translated string,
        /// or a defined fallback (and logs a warning) when the key is unknown.
        /// Never returns null or empty for a non-empty key.
        /// </summary>
        string Resolve(string key, string locale);
    }
}
