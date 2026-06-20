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
            int count = 0;

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                var flags = node.LocalizedAssetFlags;
                bool wantsText = (flags & GraphCore.LocalizedAssetFlags.Text) != 0;
                bool hasAsset = flags != GraphCore.LocalizedAssetFlags.None
                             && flags != GraphCore.LocalizedAssetFlags.Text;

                if (node is DialogueLineNodeData lineNode && wantsText)
                {
                    var key = DialogueLocalizationKeys.ForLine(lineNode);
                    if (!string.IsNullOrEmpty(key))
                    { entry.AddKey(key, LocalizationKeyType.Text, defaultHint: lineNode.Title, hasLocalizedAsset: hasAsset); count++; }
                }

                if (node is ChoiceNodeData choiceNode && choiceNode.Choices != null && wantsText)
                {
                    foreach (var choice in choiceNode.Choices)
                    {
                        if (choice == null) continue;
                        var key = DialogueLocalizationKeys.ForChoice(choice);
                        if (!string.IsNullOrEmpty(key))
                        { entry.AddKey(key, LocalizationKeyType.ChoiceLabel, defaultHint: choice.Title); count++; }
                    }
                }
            }

            return count;
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
