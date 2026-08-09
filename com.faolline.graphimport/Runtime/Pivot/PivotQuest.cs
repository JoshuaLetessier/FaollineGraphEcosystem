using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>An internal, source-shape-independent representation of one quest.</summary>
    public sealed class PivotQuest
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Fields { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<PivotReference>> References { get; }
        public IReadOnlyList<PivotStep> Steps { get; internal set; } = new List<PivotStep>();
        public IReadOnlyList<PivotBranch> Branches { get; internal set; } = new List<PivotBranch>();

        public PivotQuest(string id, string name, IReadOnlyDictionary<string, string> fields,
            IReadOnlyDictionary<string, IReadOnlyList<PivotReference>> references)
        {
            Id = id;
            Name = name;
            Fields = fields;
            References = references;
        }
    }
}
