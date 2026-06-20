using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Indexes the GraphDialogue lib for the central localization builder. Auto-discovered via
    /// TypeCache (extends <see cref="BaseGraphLocalizationAdapter{TGraph}"/> with a parameterless ctor).
    /// Scans all <see cref="DialogueGraph"/> assets for line/choice keys and all <see cref="Speaker"/>
    /// assets for global speaker-name keys.
    /// </summary>
    public sealed class DialogueGraphLocalizationAdapter : BaseGraphLocalizationAdapter<DialogueGraph>
    {
        public override string LibName => "GraphDialogue";

        protected override int ExtractGraphKeys(DialogueGraph graph, LocalizationGraphEntry entry)
        {
            if (graph?.Nodes == null) return 0;

            var seen = new Dictionary<(string, LocalizationKeyType), string>();

            void Register(string key, LocalizationKeyType type, string hint)
            {
                var id = (key, type);
                if (seen.TryGetValue(id, out var existing))
                { if (string.IsNullOrEmpty(existing) && !string.IsNullOrEmpty(hint)) seen[id] = hint; }
                else seen[id] = hint ?? string.Empty;
            }

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

            foreach (var kvp in seen)
                entry.AddKey(kvp.Key.Item1, kvp.Key.Item2, defaultHint: kvp.Value);
            return seen.Count;
        }

        protected override int ExtractGlobalKeys(LocalizationDatabase database)
        {
            int count = 0;
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
                    count++;
                }
            }
            return count;
        }
    }
}
