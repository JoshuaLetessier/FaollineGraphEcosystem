using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Loads the project-wide <see cref="LocalizationSettingsAsset"/> from Resources.
    /// </summary>
    public static class LocalizationSettingsLoader
    {
        private const string DefaultAssetPath = "GraphLocalizationSettings";

        public static LocalizationSettingsAsset Load()
            => Resources.Load<LocalizationSettingsAsset>(DefaultAssetPath);

        public static string GetDefaultAssetPath() => $"Assets/Resources/{DefaultAssetPath}.asset";
    }
}
