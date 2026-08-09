using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>A validated, source-shape-independent representation of one dialogue's flow.</summary>
    public sealed class PivotDialogue
    {
        public string Id { get; }
        public string Name { get; }
        public string EntryNodeId { get; }
        public IReadOnlyDictionary<string, PivotDialogueNode> Nodes { get; }

        public PivotDialogue(string id, string name, string entryNodeId, IReadOnlyDictionary<string, PivotDialogueNode> nodes)
        {
            Id = id;
            Name = name;
            EntryNodeId = entryNodeId;
            Nodes = nodes;
        }
    }
}
