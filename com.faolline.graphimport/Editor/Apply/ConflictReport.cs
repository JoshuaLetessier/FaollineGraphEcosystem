using System.Collections.Generic;

namespace Faolline.GraphImport.Editor
{
    public enum ConflictReason
    {
        AlreadyExists,
        DuplicateTargetWithinPlan
    }

    /// <summary>One previewed asset whose proposed location collides with something else.</summary>
    public sealed class ConflictEntry
    {
        public PlanEntry PlanEntry { get; }
        public string ExistingAssetPath { get; }
        public ConflictReason Reason { get; }

        public ConflictEntry(PlanEntry planEntry, string existingAssetPath, ConflictReason reason)
        {
            PlanEntry = planEntry;
            ExistingAssetPath = existingAssetPath;
            Reason = reason;
        }
    }

    /// <summary>
    /// The single source of truth for "is this run safe to apply", consumed identically by an
    /// unattended/CI run (checks <see cref="IsClean"/>) and by a human reviewing it afterward
    /// (reads <see cref="Conflicts"/>) — FR-013.
    /// </summary>
    public sealed class ConflictReport
    {
        public IReadOnlyList<ConflictEntry> Conflicts { get; }
        public bool IsClean => Conflicts.Count == 0;

        public ConflictReport(IReadOnlyList<ConflictEntry> conflicts)
        {
            Conflicts = conflicts;
        }
    }
}
