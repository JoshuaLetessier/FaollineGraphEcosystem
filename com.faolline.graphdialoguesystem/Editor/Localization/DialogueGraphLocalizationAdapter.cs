using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Registers the GraphDialogue lib with the central localization builder.
    /// Scans all <see cref="DialogueGraph"/> assets in the project and indexes their keys into a
    /// <see cref="LocalizationDatabase"/>, then scans all <see cref="Speaker"/> assets for global keys.
    /// Keys are derived deterministically from node/choice/speaker identity via
    /// <see cref="DialogueLocalizationKeys"/> — no hand-typed string fields.
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public sealed class DialogueGraphLocalizationAdapter : IGraphLocalizationAdapter
    {
        public string LibName => "GraphDialogue";

        static DialogueGraphLocalizationAdapter()
        {
            GraphLocalizationAdapterRegistry.Register(new DialogueGraphLocalizationAdapter());
        }

        public void ScanAndIndex(LocalizationDatabase database)
        {
            int totalKeys = 0;

            // Per-graph: DialogueLineNodeData (text keys) + ChoiceNodeData (choice label keys)
            var graphGuids = AssetDatabase.FindAssets("t:DialogueGraph");
            foreach (var guid in graphGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(path);
                if (graph == null) continue;

                var entry = database.GetOrCreateGraphEntry(guid, graph.name);
                var keys = ExtractKeysFromGraph(graph);
                foreach (var (key, type, hint) in keys)
                    entry.AddKey(key, type, defaultHint: hint);
                totalKeys += keys.Count;
            }

            // Global: Speaker assets → display-name keys
            int speakerCount = 0;
            var speakerGuids = AssetDatabase.FindAssets("t:Speaker");
            foreach (var guid in speakerGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var speaker = AssetDatabase.LoadAssetAtPath<Speaker>(path);
                if (speaker == null) continue;

                var key = DialogueLocalizationKeys.ForSpeaker(speaker);
                if (!string.IsNullOrEmpty(key))
                {
                    database.AddGlobalKey(key, LocalizationKeyType.SpeakerName, speaker.DisplayNameFallback);
                    speakerCount++;
                }
            }

            totalKeys += speakerCount;
            database.Metadata.TotalGraphsScanned = graphGuids.Length;
            database.Metadata.TotalKeysFound = totalKeys;

            Debug.Log($"[DialogueGraphLocalizationAdapter] {graphGuids.Length} graphs, {totalKeys} keys ({speakerCount} speakers).");
        }

        private static List<(string key, LocalizationKeyType type, string hint)> ExtractKeysFromGraph(DialogueGraph graph)
        {
            var seen = new Dictionary<(string, LocalizationKeyType), string>();

            void Register(string key, LocalizationKeyType type, string hint)
            {
                var id = (key, type);
                if (seen.TryGetValue(id, out var existing))
                { if (string.IsNullOrEmpty(existing) && !string.IsNullOrEmpty(hint)) seen[id] = hint; }
                else seen[id] = hint ?? string.Empty;
            }

            if (graph?.Nodes == null) return new List<(string, LocalizationKeyType, string)>();

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;

                if (node is DialogueLineNodeData lineNode)
                {
                    var key = DialogueLocalizationKeys.ForLine(lineNode);
                    if (!string.IsNullOrEmpty(key))
                        Register(key, LocalizationKeyType.Text, lineNode.Title);
                }

                if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null)
                {
                    foreach (var choice in choiceNode.Choices)
                    {
                        if (choice == null) continue;
                        var key = DialogueLocalizationKeys.ForChoice(choice);
                        if (!string.IsNullOrEmpty(key))
                            Register(key, LocalizationKeyType.ChoiceLabel, choice.Title);
                    }
                }
            }

            var result = new List<(string, LocalizationKeyType, string)>(seen.Count);
            foreach (var kvp in seen)
                result.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value));
            return result;
        }
    }
}
