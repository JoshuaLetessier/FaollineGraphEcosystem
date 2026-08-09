using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>The complete, unapplied preview of a generation run. Pure data — nothing has been written to disk.</summary>
    public sealed class GenerationPlan
    {
        public IReadOnlyList<PlanEntry> Entries { get; }

        public GenerationPlan(IReadOnlyList<PlanEntry> entries)
        {
            Entries = entries;
        }
    }

    /// <summary>One asset that would be created: its kind, proposed location, and the data it would be built from.</summary>
    public sealed class PlanEntry
    {
        public string LogicalId { get; }
        public PlanEntryKind Kind { get; }
        public string ProposedPath { get; set; }
        public string SourcePivotId { get; }
        public object Data { get; }

        public PlanEntry(string logicalId, PlanEntryKind kind, string proposedPath, string sourcePivotId, object data)
        {
            LogicalId = logicalId;
            Kind = kind;
            ProposedPath = proposedPath;
            SourcePivotId = sourcePivotId;
            Data = data;
        }
    }
}
