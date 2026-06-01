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

                Debug.Log($"[GraphDialogueLocalizationBuilder] Build complete: {graphs.Count} graphs, {totalKeysFound} keys found.");
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
    }
}
