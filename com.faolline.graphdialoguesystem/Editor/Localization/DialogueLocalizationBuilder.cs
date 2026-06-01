using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Scans all DialogueGraphs in the project and builds a LocalizationDatabase.
    /// Extracts textKey, speakerKey, displayTextKey from all nodes.
    /// Menu: Faolline ▸ GraphDialogue ▸ Build Localization Tables
    /// </summary>
    public static class DialogueLocalizationBuilder
    {
        private const string DefaultDatabasePath = "Assets/Resources/GraphDialogueLocalizationDatabase.asset";

        [MenuItem("Faolline/GraphDialogue/Build Localization Tables")]
        public static void BuildAll()
        {
            try
            {
                // Phase 1: Scan & Index
                var db = GetOrCreateDatabase();
                db.Clear();

                var graphs = FindAllDialogueGraphs();
                Debug.Log($"[GraphDialogueLocalizationBuilder] Found {graphs.Count} DialogueGraphs");

                int totalKeysFound = 0;
                foreach (var (graph, path) in graphs)
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    var entry = db.GetOrCreateGraphEntry(guid, graph.name);

                    var keys = ExtractKeysFromGraph(graph);
                    foreach (var (key, type) in keys)
                        entry.AddKey(key, type);

                    totalKeysFound += keys.Count;
                }

                db.Metadata.LastBuildTime = DateTime.Now;
                db.Metadata.TotalGraphsScanned = graphs.Count;
                db.Metadata.TotalKeysFound = totalKeysFound;

                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();

                Debug.Log($"[GraphDialogueLocalizationBuilder] Phase 1 complete: {graphs.Count} graphs, {totalKeysFound} keys found.");

                // Phase 2: Sync to provider (via adapter if available)
                TrySyncToUnityLocalization(db);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GraphDialogueLocalizationBuilder] Error: {ex}");
            }
        }

        private static List<(DialogueGraph graph, string path)> FindAllDialogueGraphs()
        {
            var results = new List<(DialogueGraph, string)>();
            var guids = AssetDatabase.FindAssets("t:DialogueGraph");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DialogueGraph>(path);
                if (asset != null)
                    results.Add((asset, path));
            }
            return results;
        }

        private static HashSet<(string key, LocalizationKeyType type)> ExtractKeysFromGraph(DialogueGraph graph)
        {
            var keys = new HashSet<(string, LocalizationKeyType)>();

            if (graph == null || graph.Nodes == null || graph.Nodes.Count == 0)
                return keys;

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;

                // DialogueLineNodeData: textKey, speakerKey, expressionKey
                if (node is DialogueLineNodeData lineNode)
                {
                    if (!string.IsNullOrWhiteSpace(lineNode.TextKey))
                        keys.Add((lineNode.TextKey.Trim(), LocalizationKeyType.Text));
                    if (!string.IsNullOrWhiteSpace(lineNode.SpeakerKey))
                        keys.Add((lineNode.SpeakerKey.Trim(), LocalizationKeyType.SpeakerName));
                    // expressionKey is optional, not a translation key
                }

                // ChoiceNodeData: each DialogueChoice has DisplayTextKey
                if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null)
                {
                    foreach (var choice in choiceNode.Choices)
                    {
                        if (choice is DialogueChoice dlgChoice && !string.IsNullOrWhiteSpace(dlgChoice.DisplayTextKey))
                            keys.Add((dlgChoice.DisplayTextKey.Trim(), LocalizationKeyType.ChoiceLabel));
                    }
                }
            }

            return keys;
        }

        private static LocalizationDatabase GetOrCreateDatabase()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(DefaultDatabasePath);
            if (existing != null) return existing;

            var folder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var db = ScriptableObject.CreateInstance<LocalizationDatabase>();
            AssetDatabase.CreateAsset(db, DefaultDatabasePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GraphDialogueLocalizationBuilder] Created database at {DefaultDatabasePath}");
            return db;
        }

        private static void TrySyncToUnityLocalization(LocalizationDatabase db)
        {
            var settingsAsset = LocalizationSettingsLoader.Load();
            if (settingsAsset == null)
            {
                Debug.LogWarning("[GraphDialogueLocalizationBuilder] No LocalizationSettingsAsset found. Skipping Phase 2.\n" +
                    "To enable Unity Localization sync, create one via:\n" +
                    "  Menu: Faolline ▸ GraphDialogue ▸ Localization Settings");
                return;
            }

            if (settingsAsset.Mode != LocalizationMode.UnityLocalization)
            {
                Debug.Log("[GraphDialogueLocalizationBuilder] Mode is CSV. Skipping Phase 2 (Unity Localization sync).");
                return;
            }

            // Try to call the syncer from the Unity Localization adapter via reflection
            var syncerType = Type.GetType("Faolline.GraphDialogue.Localization.Unity.Editor.UnityLocalizationDatabaseSyncer, com.faolline.graphdialoguesystem.Localization.Unity.Editor");
            if (syncerType == null)
            {
                Debug.LogWarning("[GraphDialogueLocalizationBuilder] Unity Localization adapter not available (com.unity.localization not installed?).\n" +
                    "Skipping Phase 2 sync to String Tables.");
                return;
            }

            var syncMethod = syncerType.GetMethod("SyncDatabase", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (syncMethod == null)
            {
                Debug.LogError("[GraphDialogueLocalizationBuilder] Failed to find SyncDatabase method in UnityLocalizationDatabaseSyncer.");
                return;
            }

            try
            {
                syncMethod.Invoke(null, new object[] { db });
                Debug.Log("[GraphDialogueLocalizationBuilder] Phase 2 complete: synced to Unity Localization String Tables.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GraphDialogueLocalizationBuilder] Phase 2 sync failed: {ex}");
            }
        }
    }
}
