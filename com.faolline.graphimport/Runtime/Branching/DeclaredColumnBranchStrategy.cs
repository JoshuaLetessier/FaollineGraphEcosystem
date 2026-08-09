using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Groups a quest's steps by their shared position. A group of one is a plain linear step.
    /// A group of more than one is only a valid branch when every member declares a distinct,
    /// non-null outcome — otherwise it's an error, never a guess.
    /// </summary>
    public sealed class DeclaredColumnBranchStrategy : IBranchDetectionStrategy
    {
        public (IReadOnlyList<PivotStep> Linear, IReadOnlyList<PivotBranch> Branches) Detect(PivotQuest quest, IReadOnlyList<PivotStep> steps)
        {
            var linear = new List<PivotStep>();
            var branches = new List<PivotBranch>();

            foreach (var group in steps.GroupBy(s => s.Order).OrderBy(g => g.Key))
            {
                var members = group.ToList();
                if (members.Count == 1)
                {
                    linear.Add(members[0]);
                    continue;
                }

                if (members.Any(s => string.IsNullOrEmpty(s.BranchOutcome)))
                    throw new BranchDetectionException(quest, group.Key, BranchDetectionReason.MissingOutcome);

                if (members.Select(s => s.BranchOutcome).Distinct().Count() != members.Count)
                    throw new BranchDetectionException(quest, group.Key, BranchDetectionReason.DuplicateOutcome);

                branches.Add(new PivotBranch(quest, group.Key, members));
            }

            return (linear, branches);
        }
    }
}
