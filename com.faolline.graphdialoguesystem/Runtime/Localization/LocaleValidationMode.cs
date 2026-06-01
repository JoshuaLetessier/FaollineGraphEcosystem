namespace Faolline.GraphDialogue
{
    /// <summary>
    /// How the table builder reacts to per-locale translation gaps (a key present but with an empty
    /// value in one or more locales) when generating/validating localization tables.
    /// </summary>
    public enum LocaleValidationMode
    {
        /// <summary>Accept gaps silently (early development).</summary>
        Permissive = 0,

        /// <summary>Log warnings for gaps but never block the build. Default — catches issues, keeps iterating.</summary>
        Warn = 1,

        /// <summary>Treat gaps as errors (pre-release QA gate): the build reports them as errors.</summary>
        Strict = 2,
    }
}
