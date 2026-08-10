using System;
using System.Collections.Generic;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphStandard.Editor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Builds a graphdialoguesystem <see cref="DialogueGraph"/> asset from a <see cref="PivotDialogue"/>,
    /// via <see cref="DialogueGraphBuilder"/> — never hand-assembling node data directly, so a built
    /// dialogue plays exactly as the builder already guarantees. Two passes: nodes first (so every
    /// node handle exists), then edges (so a "next" pointing to a not-yet-created node still resolves).
    ///
    /// Every generated node's <c>Id</c> is overridden to <see cref="StableNodeId"/> instead of the
    /// builder's default fresh GUID — this is what makes a line's localization key
    /// (<c>DialogueLocalizationKeys.ForLine</c>, derived from the node's own Id) predictable ahead of
    /// time from data the authoring tool already has, rather than only knowable after generation. It
    /// also means re-authoring a dialogue and regenerating it (after deleting the stale asset, per
    /// this pipeline's never-overwrite rule) reproduces the SAME keys, instead of orphaning every
    /// existing translation on each regeneration.
    /// </summary>
    public sealed class DialogueAssetGenerator : IAssetGenerator
    {
        readonly IProjectAssetResolver _resolver;

        public DialogueAssetGenerator(IProjectAssetResolver resolver = null)
        {
            _resolver = resolver ?? new NullProjectAssetResolver();
        }

        /// <summary>
        /// A node id is only guaranteed unique WITHIN its own dialogue (DialoguePivotBuilder's own
        /// rule) — namespacing with the dialogue's id (itself required unique across one export set,
        /// since sub-dialogue reference resolution already depends on that) prevents two different
        /// dialogues' same-named nodes from colliding on the same localization key.
        /// </summary>
        public static string StableNodeId(PivotDialogue dialogue, string pivotNodeId) => $"{dialogue.Id}_{pivotNodeId}";

        public void Generate(PlanEntry entry)
        {
            var dialogue = (PivotDialogue)entry.Data;
            var builder = new DialogueGraphBuilder();

            var handleById = new Dictionary<string, DialogueNodeHandle>();
            var pendingOptions = new List<(DialogueOptionHandle option, string targetId)>();
            var speakerKeys = new HashSet<string>();

            foreach (var node in dialogue.Nodes.Values)
            {
                DialogueNodeHandle handle;
                var stableId = StableNodeId(dialogue, node.Id);

                switch (node)
                {
                    case PivotLine line:
                        speakerKeys.Add(line.SpeakerKey);
                        var lineHandle = builder.AddLine(line.SpeakerKey, line.Text).Id(stableId);
                        if (node.Id == dialogue.EntryNodeId) lineHandle.AsEntry();
                        handle = lineHandle;
                        break;

                    case PivotChoice choice:
                        var choiceHandle = builder.AddChoice().Id(stableId);
                        foreach (var option in choice.Options)
                            pendingOptions.Add((choiceHandle.Option(option.Label), option.Next));
                        if (node.Id == dialogue.EntryNodeId) choiceHandle.AsEntry();
                        handle = choiceHandle;
                        break;

                    case PivotEnd end:
                        var endHandle = builder.AddEnd(ParseReason(end.Reason), end.OutcomeLabel).Id(stableId);
                        if (node.Id == dialogue.EntryNodeId) endHandle.AsEntry();
                        handle = endHandle;
                        break;

                    case PivotSubDialogueLink link:
                        var target = _resolver.ResolveGraph(link.TargetDialogueRef.TargetTable, link.TargetDialogueRef.TargetId);
                        var subHandle = builder.AddSubGraph(link.Id, target).Id(stableId);
                        if (node.Id == dialogue.EntryNodeId) subHandle.AsEntry();
                        handle = subHandle;
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled pivot dialogue node type '{node.GetType().Name}'.");
                }

                handleById[node.Id] = handle;
            }

            foreach (var (option, targetId) in pendingOptions)
                option.To(handleById[targetId]);

            foreach (var node in dialogue.Nodes.Values)
            {
                if (node is PivotLine line && line.Next != null)
                    ((DialogueLineHandle)handleById[node.Id]).To(handleById[line.Next]);
                if (node is PivotSubDialogueLink link && link.Next != null)
                    ((DialogueSubGraphHandle)handleById[node.Id]).To(handleById[link.Next]);
            }

            foreach (var speakerKey in speakerKeys)
            {
                var speaker = _resolver.ResolveSpeaker(speakerKey);
                if (speaker != null)
                    builder.WithSpeaker(speaker);
            }

            var graph = builder.Build();
            GraphAssetBuilder.Save(graph, entry.ProposedPath);
        }

        static EndReason ParseReason(string raw)
        {
            if (Enum.TryParse<EndReason>(raw, ignoreCase: true, out var reason))
                return reason;
            throw new InvalidOperationException($"Unrecognized dialogue end reason '{raw}'.");
        }
    }
}
