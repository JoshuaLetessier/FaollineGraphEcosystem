namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Which Unity Localization tables the build generates (Mode = UnityLocalization).
    /// </summary>
    public enum UnityTableMode
    {
        /// <summary>String Tables only (classic text). Default.</summary>
        Text,
        /// <summary>Asset Tables only (localized audio/assets) — assumes String Tables are managed elsewhere.</summary>
        Asset,
        /// <summary>Both String Tables and mirror Asset Tables.</summary>
        Both,
    }
}
