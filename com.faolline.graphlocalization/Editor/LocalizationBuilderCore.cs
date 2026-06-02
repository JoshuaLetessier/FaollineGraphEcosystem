using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Central entry point for building all localization databases in the project. Iterates every
    /// registered <see cref="IGraphLocalizationAdapter"/> and delegates scanning + syncing to it,
    /// creating one <see cref="LocalizationDatabase"/> per lib under Assets/Resources/.
    /// Unity Localization sync (Phase 2) is dispatched to the adapter assembly via reflection so
    /// this core assembly stays free of the com.unity.localization dependency.
    /// Menu: Faolline ▸ Localization ▸ Build All Tables
    /// </summary>
    public static class LocalizationBuilderCore
    {
        private const string CollectionsRoot = "Assets/Localization/Collections";

        [MenuItem("Faolline/Localization/Build All Tables")]
        public static void BuildAll()
        {
            var adapters = GraphLocalizationAdapterRegistry.DiscoverAdapters();
            if (adapters.Count == 0)
            {
                Debug.LogWarning("[LocalizationBuilderCore] No IGraphLocalizationAdapter found in the project. " +
                    "Implement one (e.g. DialogueGraphLocalizationAdapter) so your graphs can be indexed.");
                return;
            }

            var settingsAsset = LocalizationSettingsLoader.Load();
            var validation = settingsAsset?.LocaleValidation ?? LocaleValidationMode.Warn;

            foreach (var adapter in adapters)
            {
                try
                {
                    BuildForAdapter(adapter, validation);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalizationBuilderCore] Error building '{adapter.LibName}': {ex}");
                }
            }

            Debug.Log($"[LocalizationBuilderCore] Done. {adapters.Count} lib(s) processed.");

            // Refresh the dashboard if it is already open.
            if (EditorWindow.HasOpenInstances<LocalizationDashboardWindow>())
                EditorWindow.GetWindow<LocalizationDashboardWindow>().Refresh();
        }

        private static void BuildForAdapter(IGraphLocalizationAdapter adapter, LocaleValidationMode validation)
        {
            // Phase 1: scan + index
            var db = GetOrCreateDatabase(adapter.LibName);
            db.Clear();
            adapter.ScanAndIndex(db);

            db.Metadata.LastBuildTime = DateTime.Now;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LocalizationBuilderCore] [{adapter.LibName}] Phase 1: {db.Graphs.Count} graphs, " +
                $"{db.Metadata.TotalKeysFound} keys, {db.GlobalKeys.Count} global keys.");

            // Phase 2: export to the configured backend
            var settingsAsset = LocalizationSettingsLoader.Load();
            var mode = settingsAsset?.Mode ?? LocalizationMode.Csv;

            if (mode == LocalizationMode.UnityLocalization)
            {
                // Unity Localization sync (via reflection — keeps this assembly dependency-free)
                TrySyncToUnityLocalization(adapter.LibName, db, validation);
            }
            else
            {
                // CSV export
                var locales = settingsAsset != null ? settingsAsset.CsvLocales : new[] { "en" };
                var sourceLocale = locales != null && locales.Count > 0 ? locales[0] : "en";
                var folder = settingsAsset != null ? settingsAsset.CsvOutputFolder : "Assets/Localization/Csv";
                CsvLocalizationExporter.Export(adapter.LibName, db, locales, sourceLocale, folder, validation);
            }
        }

        private static LocalizationDatabase GetOrCreateDatabase(string libName)
        {
            var safeName = SanitizeFileName(libName);
            var path = $"Assets/Resources/GraphLocalization_{safeName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var db = ScriptableObject.CreateInstance<LocalizationDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LocalizationBuilderCore] Created database at {path}");
            return db;
        }

        private static void TrySyncToUnityLocalization(string libName, LocalizationDatabase db, LocaleValidationMode validation)
        {
            var settingsAsset = LocalizationSettingsLoader.Load();
            if (settingsAsset == null)
            {
                Debug.LogWarning($"[LocalizationBuilderCore] [{libName}] No LocalizationSettingsAsset found. " +
                    "Create one via Faolline ▸ Localization ▸ Localization Settings. Skipping Phase 2.");
                return;
            }

            if (settingsAsset.Mode != LocalizationMode.UnityLocalization)
            {
                Debug.Log($"[LocalizationBuilderCore] [{libName}] Mode is CSV. Skipping Phase 2.");
                return;
            }

            var syncerType = Type.GetType(
                "Faolline.GraphLocalization.Unity.Editor.UnityLocalizationSyncer, " +
                "com.faolline.graphlocalization.Localization.Unity.Editor");

            if (syncerType == null)
            {
                Debug.LogWarning($"[LocalizationBuilderCore] [{libName}] Unity Localization adapter not found. Skipping Phase 2.");
                return;
            }

            var method = syncerType.GetMethod("SyncDatabase",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogError($"[LocalizationBuilderCore] [{libName}] SyncDatabase method not found.");
                return;
            }

            method.Invoke(null, new object[] { libName, db, validation });
            Debug.Log($"[LocalizationBuilderCore] [{libName}] Phase 2 complete.");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = System.Array.ConvertAll(name.ToCharArray(), c => System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return new string(chars);
        }
    }
}
