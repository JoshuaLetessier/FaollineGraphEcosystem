using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLogging.Editor
{
    /// <summary>Creates (if missing) and selects the project-wide <see cref="GraphLoggingSettings"/> asset.</summary>
    public static class GraphLoggingMenu
    {
        [MenuItem("Faolline/Diagnostics/Log Settings")]
        public static void OpenSettings()
        {
            var path = GraphLoggingSettingsLoader.GetDefaultAssetPath();
            var existing = AssetDatabase.LoadAssetAtPath<GraphLoggingSettings>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<GraphLoggingSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
