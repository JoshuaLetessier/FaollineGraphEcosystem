using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Creates exactly the plan entries absent from the conflict report. Never overwrites, never
    /// silently drops a conflicting entry — those are simply left for the report to describe (FR-012).
    /// A generator failure on one entry is caught and recorded, never aborting the rest of the run —
    /// the first-run-on-an-empty-project case (missing destination folders) must not take down every
    /// other entry that would otherwise have succeeded.
    /// </summary>
    public static class PlanApplier
    {
        public static ApplyResult Apply(GenerationPlan plan, ConflictReport report,
            IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> generators)
        {
            var conflictingIds = new HashSet<string>(report.Conflicts.Select(c => c.PlanEntry.LogicalId));
            var created = new List<PlanEntry>();
            var failures = new List<GenerationFailure>();

            foreach (var entry in plan.Entries)
            {
                if (conflictingIds.Contains(entry.LogicalId))
                    continue;

                try
                {
                    EnsureDestinationFolderExists(entry.ProposedPath);
                    generators[entry.Kind].Generate(entry);
                    created.Add(entry);
                }
                catch (Exception ex)
                {
                    failures.Add(new GenerationFailure(entry, ex));
                }
            }

            return new ApplyResult(created, failures);
        }

        /// <summary>
        /// AssetDatabase.CreateAsset (used by every generator) requires every ancestor folder to
        /// already exist — it does not create them. On a brand-new project with no prior Graphs/
        /// folder this is the nominal first run, not an edge case, so the folder chain is built here
        /// once, up front, rather than leaving each generator to duplicate this or crash.
        /// </summary>
        static void EnsureDestinationFolderExists(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            var parts = directory.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
