using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Splits a quest's steps into purely linear ones and branches. Never infers a branch from
    /// step names/text (FR-005) — only from whatever this strategy is explicitly given to detect.
    /// </summary>
    public interface IBranchDetectionStrategy
    {
        (IReadOnlyList<PivotStep> Linear, IReadOnlyList<PivotBranch> Branches) Detect(PivotQuest quest, IReadOnlyList<PivotStep> steps);
    }
}
