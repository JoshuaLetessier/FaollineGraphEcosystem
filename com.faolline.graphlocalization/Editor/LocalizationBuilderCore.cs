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
        // Must match UnityLocalizationSyncer.AssetCollectionSuffix (that type is in the gated assembly).
        private const string AssetCollectionSuffix = "_Assets";

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

            var manifest = GetOrCreateManifest();

            foreach (var adapter in adapters)
            {
                try
                {
                    BuildForAdapter(adapter, validation, manifest);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalizationBuilderCore] Error building '{adapter.LibName}': {ex}");
                }
            }

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LocalizationBuilderCore] Done. {adapters.Count} lib(s) processed.");

            // Refresh the dashboard if it is already open.
            if (EditorWindow.HasOpenInstances<LocalizationDashboardWindow>())
                EditorWindow.GetWindow<LocalizationDashboardWindow>().Refresh();
        }

        private static void BuildForAdapter(IGraphLocalizationAdapter adapter, LocaleValidationMode validation,
            GraphLocalizationManifest manifest)
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

            // Phase 2: export to the configured backend, recording the produced artifacts in the manifest
            // so the runtime providers can find keys spread across per-graph collections/files.
            var settingsAsset = LocalizationSettingsLoader.Load();
            var mode = settingsAsset?.Mode ?? LocalizationMode.Csv;
            var libEntry = manifest.GetOrCreateLib(adapter.LibName);
            libEntry.UnityCollections.Clear();
            libEntry.UnityAssetCollections.Clear();
            libEntry.CsvFiles.Clear();

            if (mode == LocalizationMode.UnityLocalization)
            {
                // Unity Localization sync (via reflection — keeps this assembly dependency-free)
                bool genText = settingsAsset == null || settingsAsset.GeneratesStringTables;
                bool genAsset = settingsAsset != null && settingsAsset.GeneratesAssetTables;
                var collections = TrySyncToUnityLocalization(adapter.LibName, db, validation, genText, genAsset);
                if (collections != null)
                {
                    // Always record the String Table collection names so text resolves (even in Asset-only
                    // mode, where the tables exist from a previous Text/Both build).
                    libEntry.UnityCollections.AddRange(collections);
                    if (genAsset)
                        foreach (var c in collections)
                            libEntry.UnityAssetCollections.Add(c + AssetCollectionSuffix); // mirror naming
                }
            }
            else
            {
                // CSV export
                var locales = settingsAsset != null ? settingsAsset.CsvLocales : new[] { "en" };
                var sourceLocale = locales != null && locales.Count > 0 ? locales[0] : "en";
                var folder = settingsAsset != null ? settingsAsset.CsvOutputFolder : "Assets/Localization/Csv";
                var paths = CsvLocalizationExporter.Export(adapter.LibName, db, locales, sourceLocale, folder, validation);
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null) libEntry.CsvFiles.Add(asset);
                }
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

        private static string[] TrySyncToUnityLocalization(string libName, LocalizationDatabase db,
            LocaleValidationMode validation, bool generateStringTables, bool generateAssetTables)
        {
            var settingsAsset = LocalizationSettingsLoader.Load();
            if (settingsAsset == null)
            {
                Debug.LogWarning($"[LocalizationBuilderCore] [{libName}] No LocalizationSettingsAsset found. " +
                    "Create one via Faolline ▸ Localization ▸ Localization Settings. Skipping Phase 2.");
                return null;
            }

            if (settingsAsset.Mode != LocalizationMode.UnityLocalization)
            {
                Debug.Log($"[LocalizationBuilderCore] [{libName}] Mode is CSV. Skipping Phase 2.");
                return null;
            }

            var syncerType = Type.GetType(
                "Faolline.GraphLocalization.Unity.Editor.UnityLocalizationSyncer, " +
                "com.faolline.graphlocalization.Localization.Unity.Editor");

            if (syncerType == null)
            {
                Debug.LogWarning($"[LocalizationBuilderCore] [{libName}] Unity Localization adapter not found. Skipping Phase 2.");
                return null;
            }

            var method = syncerType.GetMethod("SyncDatabase",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogError($"[LocalizationBuilderCore] [{libName}] SyncDatabase method not found.");
                return null;
            }

            var result = method.Invoke(null, new object[] { libName, db, validation, generateStringTables, generateAssetTables }) as string[];
            Debug.Log($"[LocalizationBuilderCore] [{libName}] Phase 2 complete. " +
                $"Collections: {(result != null ? string.Join(", ", result) : "(none)")}");
            return result;
        }

        private static GraphLocalizationManifest GetOrCreateManifest()
        {
            var path = $"Assets/Resources/{GraphLocalizationManifest.ResourceName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GraphLocalizationManifest>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var manifest = ScriptableObject.CreateInstance<GraphLocalizationManifest>();
            AssetDatabase.CreateAsset(manifest, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LocalizationBuilderCore] Created localization manifest at {path}");
            return manifest;
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
