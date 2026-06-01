using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

#if GRAPHDIALOGUE_UNITY_LOCALIZATION
using UnityEditor.Localization;
using UnityEngine.Localization;
#endif

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

                // Phase 2: Sync to provider
                var mode = LocalizationContext.Current.Mode;
#if GRAPHDIALOGUE_UNITY_LOCALIZATION
                if (mode == LocalizationMode.UnityLocalization)
                {
                    SyncToUnityLocalization(db);
                    Debug.Log("[GraphDialogueLocalizationBuilder] Phase 2 complete: synced to Unity Localization String Tables.");
                }
#endif
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

#if GRAPHDIALOGUE_UNITY_LOCALIZATION
        private static void SyncToUnityLocalization(LocalizationDatabase db)
        {
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning("[GraphDialogueLocalizationBuilder] No locales configured in Project Settings > Localization");
                return;
            }

            // Create collections per graph
            foreach (var graphEntry in db.Graphs)
            {
                var collectionName = $"DLG_{Sanitize(graphEntry.GraphName)}";
                var collection = GetOrCreateCollection(collectionName);
                EnsureTablesForAllLocales(collection, locales);
                SyncEntries(collection, graphEntry.Keys);
            }

            // Create global Speakers collection
            var speakersCollection = GetOrCreateCollection("Dialogue_Speakers");
            EnsureTablesForAllLocales(speakersCollection, locales);

            AssetDatabase.SaveAssets();
        }

        private static UnityEditor.Localization.StringTableCollection GetOrCreateCollection(string collectionName)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (existing != null) return existing;

            var folder = "Assets/Localization/Collections";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(AssetDatabase.IsValidFolder("Assets/Localization") ? "Assets/Localization" : "Assets",
                    AssetDatabase.IsValidFolder("Assets/Localization") ? "Collections" : "Localization");

            var collection = LocalizationEditorSettings.CreateStringTableCollection(collectionName, $"{folder}/{collectionName}");
            Debug.Log($"[GraphDialogueLocalizationBuilder] Created String Table Collection: {collectionName}");
            return collection;
        }

        private static void EnsureTablesForAllLocales(UnityEditor.Localization.StringTableCollection collection, IEnumerable<Locale> locales)
        {
            foreach (var locale in locales)
            {
                var table = collection.GetTable(locale.Identifier);
                if (table == null)
                    collection.AddNewTable(locale.Identifier);
            }
        }

        private static void SyncEntries(UnityEditor.Localization.StringTableCollection collection, IReadOnlyList<LocalizationKeyEntry> keys)
        {
            var shared = collection.SharedData;
            foreach (var keyEntry in keys)
            {
                if (string.IsNullOrWhiteSpace(keyEntry.Key)) continue;

                var existing = shared.GetEntry(keyEntry.Key);
                if (existing == null)
                    shared.AddKey(keyEntry.Key);
            }

            EditorUtility.SetDirty(shared);
            foreach (var table in collection.StringTables)
                if (table != null) EditorUtility.SetDirty(table);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
#endif
    }
}
