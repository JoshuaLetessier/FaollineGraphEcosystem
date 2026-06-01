using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>Creates or selects the project-wide LocalizationSettingsAsset.</summary>
    public static class LocalizationSettingsMenu
    {
        [MenuItem("Faolline/Localization/Localization Settings")]
        public static void OpenOrCreateSettings()
        {
            var path = LocalizationSettingsLoader.GetDefaultAssetPath();
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return;
            }

            var folder = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;

            Debug.Log($"[GraphLocalization] Created LocalizationSettingsAsset at {path}");
        }
    }
}
