using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Loads the project-wide <see cref="LocalizationSettingsAsset"/> from a default location.
    /// Path: <c>Assets/Resources/GraphDialogueLocalizationSettings.asset</c>.
    /// If not found, returns null and a safe default is used by <see cref="LocalizationContext"/>.
    /// </summary>
    public static class LocalizationSettingsLoader
    {
        private const string DefaultAssetPath = "GraphDialogueLocalizationSettings";

        /// <summary>Loads the asset from Resources, or null if not found.</summary>
        public static LocalizationSettingsAsset Load()
        {
            return Resources.Load<LocalizationSettingsAsset>(DefaultAssetPath);
        }

        /// <summary>Gets the resource path where the asset should be created.</summary>
        public static string GetDefaultAssetPath() => $"Assets/Resources/{DefaultAssetPath}.asset";
    }
}
