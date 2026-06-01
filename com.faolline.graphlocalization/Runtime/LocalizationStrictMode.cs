namespace Faolline.GraphLocalization
{
    /// <summary>
    /// How the runtime player reacts when a localization key cannot be resolved during playback.
    /// </summary>
    public enum LocalizationStrictMode
    {
        /// <summary>Use the fallback silently (early development / shipping safety).</summary>
        Permissive = 0,

        /// <summary>Use the fallback, log a warning, and record the key for an end-of-session report.</summary>
        Audit = 1,

        /// <summary>Throw a <see cref="LocalizationException"/> on the first missing key (QA gate).</summary>
        Strict = 2,
    }
}
