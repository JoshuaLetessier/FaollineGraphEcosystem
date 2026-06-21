using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Automatically rebuilds localization tables when a graph asset is saved.
    /// Coalesces rapid saves into a single deferred build (300 ms debounce).
    /// A reentrance guard prevents infinite loops (BuildAll itself saves assets).
    /// Disable via the AutoBuild toggle on <see cref="LocalizationSettingsAsset"/>.
    /// </summary>
    public sealed class LocalizationAutoBuilder : AssetPostprocessor
    {
        private static double _pendingBuildTime;
        private static bool _buildScheduled;
        internal static bool Building;

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Building) return;
            if (!ShouldAutoBuild()) return;

            bool graphChanged = false;
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(".asset") && IsGraphAsset(path))
                { graphChanged = true; break; }
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
            Building = true;
            try { LocalizationBuilderCore.BuildAll(); }
            finally { Building = false; }
        }

        private static bool IsGraphAsset(string path)
        {
            if (path.Contains("Localization") || path.Contains("Resources")) return false;

            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj is GraphCore.BaseGraph) return true;
            if (obj is GraphLocalizationData) return true;
            return false;
        }

        private static bool ShouldAutoBuild()
        {
            var settings = LocalizationSettingsLoader.Load();
            return settings == null || settings.AutoBuild;
        }
    }
}
