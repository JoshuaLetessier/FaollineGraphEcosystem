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
    /// </summary>
    public sealed class DialogueAssetGenerator : IAssetGenerator
    {
        readonly IProjectAssetResolver _resolver;

        public DialogueAssetGenerator(IProjectAssetResolver resolver = null)
        {
            _resolver = resolver ?? new NullProjectAssetResolver();
        }

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

                switch (node)
                {
                    case PivotLine line:
                        speakerKeys.Add(line.SpeakerKey);
                        var lineHandle = builder.AddLine(line.SpeakerKey, line.Text);
                        if (node.Id == dialogue.EntryNodeId) lineHandle.AsEntry();
                        handle = lineHandle;
                        break;

                    case PivotChoice choice:
                        var choiceHandle = builder.AddChoice();
                        foreach (var option in choice.Options)
                            pendingOptions.Add((choiceHandle.Option(option.Label), option.Next));
                        if (node.Id == dialogue.EntryNodeId) choiceHandle.AsEntry();
                        handle = choiceHandle;
                        break;

                    case PivotEnd end:
                        var endHandle = builder.AddEnd(ParseReason(end.Reason), end.OutcomeLabel);
                        if (node.Id == dialogue.EntryNodeId) endHandle.AsEntry();
                        handle = endHandle;
                        break;

                    case PivotSubDialogueLink link:
                        var target = _resolver.ResolveGraph(link.TargetDialogueRef.TargetTable, link.TargetDialogueRef.TargetId);
                        var subHandle = builder.AddSubGraph(link.Id, target);
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
