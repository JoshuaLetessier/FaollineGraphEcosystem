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
                    foreach (var (key, type, hint) in keys)
                        entry.AddKey(key, type, defaultHint: hint);

                    totalKeysFound += keys.Count;
                }

                // Scan Speaker assets for their DisplayNameKey (global, shared across graphs).
                // SpeakerKey on nodes is a logical id (not translated) — the translatable key is
                // Speaker.DisplayNameKey, which lives on the Speaker ScriptableObject.
                int speakerKeysFound = ScanSpeakerAssets(db);
                totalKeysFound += speakerKeysFound;

                db.Metadata.LastBuildTime = DateTime.Now;
                db.Metadata.TotalGraphsScanned = graphs.Count;
                db.Metadata.TotalKeysFound = totalKeysFound;

                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();

                Debug.Log($"[GraphDialogueLocalizationBuilder] Phase 1 complete: {graphs.Count} graphs, {totalKeysFound} keys found ({speakerKeysFound} speaker keys).");

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

        /// <summary>
        /// Scans all Speaker assets and registers their DisplayNameKey as global speaker keys.
        /// Returns the number of keys added.
        /// </summary>
        private static int ScanSpeakerAssets(LocalizationDatabase db)
        {
            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Speaker");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var speaker = AssetDatabase.LoadAssetAtPath<Speaker>(path);
                if (speaker == null) continue;

                if (!string.IsNullOrWhiteSpace(speaker.DisplayNameKey))
                {
                    db.AddSpeakerKey(speaker.DisplayNameKey, speaker.DisplayNameFallback);
                    count++;
                }
            }

            Debug.Log($"[GraphDialogueLocalizationBuilder] Scanned {guids.Length} Speaker assets, {count} display-name keys found.");
            return count;
        }

        private static List<(string key, LocalizationKeyType type, string hint)> ExtractKeysFromGraph(DialogueGraph graph)
        {
            // Dedupe by (key, type); keep the first non-empty source hint encountered.
            var seen = new Dictionary<(string, LocalizationKeyType), string>();

            if (graph == null || graph.Nodes == null || graph.Nodes.Count == 0)
                return new List<(string, LocalizationKeyType, string)>();

            void Register(string key, LocalizationKeyType type, string hint)
            {
                var id = (key, type);
                if (seen.TryGetValue(id, out var existingHint))
                {
                    if (string.IsNullOrEmpty(existingHint) && !string.IsNullOrEmpty(hint))
                        seen[id] = hint;
                }
                else
                {
                    seen[id] = hint ?? string.Empty;
                }
            }

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;

                // DialogueLineNodeData: only textKey is a translation key. The node Title is the
                // source/default text used to pre-fill the entry. speakerKey is a logical id (matches
                // Speaker.SpeakerId) — not translated. expressionKey is a visual selector — not translated.
                if (node is DialogueLineNodeData lineNode)
                {
                    if (!string.IsNullOrWhiteSpace(lineNode.TextKey))
                        Register(lineNode.TextKey.Trim(), LocalizationKeyType.Text, lineNode.Title);
                }

                // ChoiceNodeData: each DialogueChoice has DisplayTextKey (no source-text field yet).
                if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null)
                {
                    foreach (var choice in choiceNode.Choices)
                    {
                        if (choice is DialogueChoice dlgChoice && !string.IsNullOrWhiteSpace(dlgChoice.DisplayTextKey))
                            Register(dlgChoice.DisplayTextKey.Trim(), LocalizationKeyType.ChoiceLabel, string.Empty);
                    }
                }
            }

            var result = new List<(string, LocalizationKeyType, string)>(seen.Count);
            foreach (var kvp in seen)
                result.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value));
            return result;
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
