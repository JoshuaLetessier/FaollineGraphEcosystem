using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Checks a plan against the current project state and against itself. Never mutates anything —
    /// a pure read followed by a report (FR-012).
    /// </summary>
    public static class PlanConflictDetector
    {
        public static ConflictReport Detect(GenerationPlan plan)
        {
            var conflicts = new List<ConflictEntry>();
            var pathCounts = plan.Entries
                .GroupBy(e => e.ProposedPath)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var entry in plan.Entries)
            {
                if (pathCounts[entry.ProposedPath] > 1)
                {
                    conflicts.Add(new ConflictEntry(entry, entry.ProposedPath, ConflictReason.DuplicateTargetWithinPlan));
                    continue;
                }

                var existing = AssetDatabase.LoadAssetAtPath<Object>(entry.ProposedPath);
                if (existing != null)
                    conflicts.Add(new ConflictEntry(entry, entry.ProposedPath, ConflictReason.AlreadyExists));
            }

            return new ConflictReport(conflicts);
        }
    }
}
