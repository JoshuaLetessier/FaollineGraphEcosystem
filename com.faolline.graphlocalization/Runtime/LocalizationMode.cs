namespace Faolline.GraphLocalization
{
    /// <summary>Selects the project-wide localization backend.</summary>
    public enum LocalizationMode
    {
        /// <summary>CSV table (default, no external dependency).</summary>
        Csv = 0,

        /// <summary>Unity Localization (com.unity.localization, if installed).</summary>
        UnityLocalization = 1,
    }
}
