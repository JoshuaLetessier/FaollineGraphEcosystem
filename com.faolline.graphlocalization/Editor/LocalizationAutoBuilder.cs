using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Automatically rebuilds localization tables when a graph asset is saved.
    /// Coalesces rapid saves into a single deferred build (300 ms debounce).
    /// Disable by deleting this file or via the toggle on LocalizationSettingsAsset.
    /// </summary>
    public sealed class LocalizationAutoBuilder : AssetPostprocessor
    {
        private static double _pendingBuildTime;
        private static bool _buildScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ShouldAutoBuild()) return;

            bool graphChanged = false;
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(".asset") && IsGraphAsset(path))
                { graphChanged = true; break; }
            }
            if (!graphChanged)
            {
                foreach (var path in deletedAssets)
                {
                    if (path.EndsWith(".asset"))
                    { graphChanged = true; break; }
                }
            }

            if (!graphChanged) return;

            _pendingBuildTime = EditorApplication.timeSinceStartup + 0.3;
            if (!_buildScheduled)
            {
                _buildScheduled = true;
                EditorApplication.update += DeferredBuild;
            }
        }

        private static void DeferredBuild()
        {
            if (EditorApplication.timeSinceStartup < _pendingBuildTime) return;

            _buildScheduled = false;
            EditorApplication.update -= DeferredBuild;

            if (GraphLocalizationAdapterRegistry.DiscoverAdapters().Count == 0) return;

            Debug.Log("[GraphLocalization] Auto-rebuilding tables (graph asset changed).");
            LocalizationBuilderCore.BuildAll();
        }

        private static bool IsGraphAsset(string path)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) return false;
            var typeName = obj.GetType().Name;
            return typeName.Contains("Graph") && obj is ScriptableObject;
        }

        private static bool ShouldAutoBuild()
        {
            var settings = LocalizationSettingsLoader.Load();
            return settings == null || settings.AutoBuild;
        }
    }
}
