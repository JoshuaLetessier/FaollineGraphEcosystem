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
        public override string LibName => DialogueLocalizationKeys.LibName;

        protected override int ExtractGraphKeys(DialogueGraph graph, LocalizationGraphEntry entry)
        {
            if (graph?.Nodes == null) return 0;
            int count = 0;
            var locData = graph.LocalizationFlags;   // inline flags (ILocalizedGraph); never null

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                var flags = locData.GetFlags(node.Id);
                bool wantsText = (flags & LocalizedAssetFlags.Text) != 0;
                int rawFlags = (int)flags;

                if (node is DialogueLineNodeData lineNode && wantsText)
                {
                    var key = DialogueLocalizationKeys.ForLine(lineNode);
                    if (!string.IsNullOrEmpty(key))
                    { entry.AddKey(key, LocalizationKeyType.Text, nodeId: node.Id, defaultHint: lineNode.Title, assetFlags: rawFlags); count++; }
                }

                if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null && wantsText)
                {
                    foreach (var choice in choiceNode.Choices)
                    {
                        if (choice == null) continue;
                        var key = DialogueLocalizationKeys.ForChoice(choice);
                        if (!string.IsNullOrEmpty(key))
                        { entry.AddKey(key, LocalizationKeyType.ChoiceLabel, nodeId: node.Id, defaultHint: choice.Title, assetFlags: rawFlags); count++; }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Files each speaker's display-name key under its table group (<see cref="DialogueLocalizationKeys.SpeakerTableGroup"/>),
        /// visiting speakers in asset-path order so the outcome is deterministic. Two speakers sharing a
        /// <see cref="Speaker.SpeakerId"/> but not a group is reported as an error naming both assets; the first
        /// (path order) keeps the key, so its existing translations are not dropped.
        /// </summary>
        protected override int ExtractGlobalKeys(LocalizationDatabase database)
        {
            int count = 0;
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Speaker"))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);

            var firstById = new Dictionary<string, (string group, string path)>();
            foreach (var path in paths)
            {
                var speaker = AssetDatabase.LoadAssetAtPath<Speaker>(path);
                if (speaker == null) continue;

                var key = DialogueLocalizationKeys.ForSpeaker(speaker);
                if (string.IsNullOrEmpty(key)) continue;

                var group = DialogueLocalizationKeys.SpeakerTableGroup(speaker);
                if (firstById.TryGetValue(speaker.SpeakerId, out var first))
                {
                    if (LocalizationTableNames.GroupComparisonKey(first.group) != LocalizationTableNames.GroupComparisonKey(group))
                        Faolline.GraphLogging.Logging.Error("GraphLocalization.Validation",
                            $"[GraphDialogue] Speaker id '{speaker.SpeakerId}' is used by '{first.path}' (table group " +
                            $"'{first.group}') and '{path}' (table group '{group}'). One name key cannot live in two " +
                            $"tables: it stays in '{first.group}'. Give each speaker a unique id, or the same group.");
                    continue;
                }

                firstById[speaker.SpeakerId] = (group, path);
                database.AddGlobalKey(key, LocalizationKeyType.SpeakerName, speaker.DisplayNameFallback, group);
                count++;
            }
            return count;
        }
    }
}
