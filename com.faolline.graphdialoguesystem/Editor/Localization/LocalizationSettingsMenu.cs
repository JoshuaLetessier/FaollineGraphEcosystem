using UnityEditor;
using UnityEngine;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Editor menu to create/configure the project-wide localization settings asset.
    /// Menu: Faolline ▸ GraphDialogue ▸ Localization Settings
    /// </summary>
    public static class LocalizationSettingsMenu
    {
        [MenuItem("Faolline/GraphDialogue/Localization Settings")]
        public static void OpenOrCreateSettings()
        {
            var asset = LocalizationSettingsLoader.Load();

            if (asset == null)
            {
                // Create new asset in Resources
                var resourcesPath = "Assets/Resources";
                if (!AssetDatabase.IsValidFolder(resourcesPath))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
                AssetDatabase.CreateAsset(asset, LocalizationSettingsLoader.GetDefaultAssetPath());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[GraphDialogue] Created localization settings at {LocalizationSettingsLoader.GetDefaultAssetPath()}");
            }

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
