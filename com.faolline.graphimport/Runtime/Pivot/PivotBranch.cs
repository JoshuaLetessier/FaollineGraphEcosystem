using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>A point where a quest's flow diverges: multiple steps at the same position, each gated by a declared outcome.</summary>
    public sealed class PivotBranch
    {
        public PivotQuest Quest { get; }
        public int Position { get; }
        public IReadOnlyList<PivotStep> Steps { get; }

        public PivotBranch(PivotQuest quest, int position, IReadOnlyList<PivotStep> steps)
        {
            Quest = quest;
            Position = position;
            Steps = steps;
        }
    }
}
