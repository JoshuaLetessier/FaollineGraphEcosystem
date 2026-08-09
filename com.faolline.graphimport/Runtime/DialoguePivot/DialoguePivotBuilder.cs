using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Turns a raw <see cref="InterchangeDialogueSet"/> into validated <see cref="PivotDialogue"/>s:
    /// unique node ids, a matching entry point, no dangling "next" references (FR-006), sub-dialogue
    /// targets resolved within the set, and no sub-dialogue reference cycle (FR-007) — all before any
    /// asset is touched.
    /// </summary>
    public sealed class DialoguePivotBuilder
    {
        public IReadOnlyList<PivotDialogue> Build(InterchangeDialogueSet interchange)
        {
            var pivots = new List<PivotDialogue>();

            foreach (var dialogue in interchange.Dialogues)
            {
                var nodes = new Dictionary<string, PivotDialogueNode>();
                var seenIds = new HashSet<string>();

                foreach (var rawNode in dialogue.Nodes)
                {
                    if (!seenIds.Add(rawNode.Id))
                        throw new DialogueStructureException(dialogue.Id, rawNode.Id, DialogueStructureIssue.DuplicateNodeId);

                    nodes[rawNode.Id] = ConvertNode(dialogue, rawNode, interchange.Dialogues);
                }

                if (!nodes.ContainsKey(dialogue.EntryNodeId))
                    throw new DialogueStructureException(dialogue.Id, dialogue.EntryNodeId, DialogueStructureIssue.InvalidEntryNode);

                foreach (var node in nodes.Values)
                foreach (var next in NextRefs(node))
                    if (!nodes.ContainsKey(next))
                        throw new DialogueStructureException(dialogue.Id, node.Id, DialogueStructureIssue.DanglingNext);

                pivots.Add(new PivotDialogue(dialogue.Id, dialogue.Name, dialogue.EntryNodeId, nodes));
            }

            DetectSubDialogueCycles(pivots);

            return pivots;
        }

        static PivotDialogueNode ConvertNode(InterchangeDialogue dialogue, InterchangeNode node, IReadOnlyList<InterchangeDialogue> allDialogues)
        {
            switch (node.Kind)
            {
                case InterchangeNodeKind.Line:
                    return new PivotLine(node.Id, node.SpeakerKey, node.Text, node.Next);

                case InterchangeNodeKind.Choice:
                    var options = node.Options.Select(o => new PivotChoiceOption(o.Label, o.Next)).ToList();
                    return new PivotChoice(node.Id, options);

                case InterchangeNodeKind.End:
                    return new PivotEnd(node.Id, node.Reason, node.OutcomeLabel);

                default: // SubDialogue
                    var target = ResolveDialogueRef(dialogue.Id, node.Id, node.TargetDialogue, allDialogues);
                    return new PivotSubDialogueLink(node.Id, new PivotReference("Dialogues", target.Id), node.Next);
            }
        }

        static InterchangeDialogue ResolveDialogueRef(string fromDialogueId, string fromNodeId, string raw, IReadOnlyList<InterchangeDialogue> allDialogues)
        {
            var byId = allDialogues.Where(d => d.Id == raw).ToList();
            var candidates = byId.Count > 0 ? byId : allDialogues.Where(d => d.Name == raw).ToList();

            if (candidates.Count == 0)
                throw new DialogueReferenceException(fromDialogueId, fromNodeId, raw, DialogueReferenceReason.NotFound);
            if (candidates.Count > 1)
                throw new DialogueReferenceException(fromDialogueId, fromNodeId, raw, DialogueReferenceReason.Ambiguous);

            return candidates[0];
        }

        static IEnumerable<string> NextRefs(PivotDialogueNode node)
        {
            if (node is PivotLine line && line.Next != null) yield return line.Next;
            if (node is PivotChoice choice)
                foreach (var option in choice.Options)
                    if (option.Next != null)
                        yield return option.Next;
            if (node is PivotSubDialogueLink link && link.Next != null) yield return link.Next;
        }

        static void DetectSubDialogueCycles(IReadOnlyList<PivotDialogue> pivots)
        {
            var byId = pivots.ToDictionary(p => p.Id);
            var visited = new HashSet<string>();
            var inStack = new HashSet<string>();

            foreach (var dialogue in pivots)
                Visit(dialogue.Id);

            void Visit(string dialogueId)
            {
                if (inStack.Contains(dialogueId))
                    throw new DialogueCycleException(dialogueId);
                if (!visited.Add(dialogueId))
                    return;

                inStack.Add(dialogueId);
                if (byId.TryGetValue(dialogueId, out var dialogue))
                    foreach (var link in dialogue.Nodes.Values.OfType<PivotSubDialogueLink>())
                        Visit(link.TargetDialogueRef.TargetId);
                inStack.Remove(dialogueId);
            }
        }
    }
}
