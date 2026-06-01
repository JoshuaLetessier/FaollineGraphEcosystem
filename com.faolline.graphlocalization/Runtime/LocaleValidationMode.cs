namespace Faolline.GraphLocalization
{
    /// <summary>
    /// How the table builder reacts to per-locale translation gaps when generating/validating tables.
    /// </summary>
    public enum LocaleValidationMode
    {
        /// <summary>Accept gaps silently (early development).</summary>
        Permissive = 0,

        /// <summary>Log warnings for gaps but never block the build. Default.</summary>
        Warn = 1,

        /// <summary>Log gaps as errors (pre-release QA gate).</summary>
        Strict = 2,
    }
}
