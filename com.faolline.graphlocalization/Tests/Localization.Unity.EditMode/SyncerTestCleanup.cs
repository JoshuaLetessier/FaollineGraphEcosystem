using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// Removes every table collection created under a throwaway test folder. Each collection asset is deleted
    /// on its own first, so Unity Localization's delete hook unregisters it from the project (Addressables
    /// entries, collection cache) — deleting only the folder would leave dangling registrations behind.
    /// </summary>
    internal static class SyncerTestCleanup
    {
        public static void DeleteCollectionsUnder(string root)
        {
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root)) return;

            var paths = new List<string>();
            foreach (var col in LocalizationEditorSettings.GetStringTableCollections())
                if (col != null) Add(paths, AssetDatabase.GetAssetPath(col), root);
            foreach (var col in LocalizationEditorSettings.GetAssetTableCollections())
                if (col != null) Add(paths, AssetDatabase.GetAssetPath(col), root);

            foreach (var path in paths) AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(root);
        }

        private static void Add(List<string> paths, string path, string root)
        {
            if (!string.IsNullOrEmpty(path) && path.StartsWith(root + "/", StringComparison.Ordinal)) paths.Add(path);
        }
    }
}
