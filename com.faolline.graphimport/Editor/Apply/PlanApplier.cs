using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Creates exactly the plan entries absent from the conflict report. Never overwrites, never
    /// silently drops a conflicting entry — those are simply left for the report to describe (FR-012).
    /// </summary>
    public static class PlanApplier
    {
        public static IReadOnlyList<PlanEntry> Apply(GenerationPlan plan, ConflictReport report,
            IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> generators)
        {
            var conflictingIds = new HashSet<string>(report.Conflicts.Select(c => c.PlanEntry.LogicalId));
            var created = new List<PlanEntry>();

            foreach (var entry in plan.Entries)
            {
                if (conflictingIds.Contains(entry.LogicalId))
                    continue;

                generators[entry.Kind].Generate(entry);
                created.Add(entry);
            }

            return created;
        }
    }
}
