namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Neutral text-resolution contract the dialogue runtime depends on, independent of any specific
    /// localization technology (Constitution v1.2.0: Unity Localization, if used, lives behind this
    /// interface in an optional adapter). Implementations resolve a text key into a displayed string
    /// for a locale, and MUST return a defined, non-empty fallback (never null/empty) when a key is
    /// absent in the requested locale.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>The currently active locale code (e.g. "en", "fr"). Never null/empty.</summary>
        string CurrentLocale { get; }

        /// <summary>
        /// Resolves <paramref name="key"/> in <paramref name="locale"/>. Returns the translated string,
        /// or a defined fallback (and logs a <c>[GraphDialogue]</c> warning) when the key is unknown.
        /// Never returns null or empty for a non-empty key.
        /// </summary>
        string Resolve(string key, string locale);
    }
}
