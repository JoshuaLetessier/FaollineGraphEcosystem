using System.Collections.Generic;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Single unattended/CI entry point: mapping + tables -> pivot -> plan -> conflict detection -> apply.
    /// A non-clean returned report (<see cref="ConflictReport.IsClean"/> == false) is the one signal a
    /// CI script needs to fail the job — the same report a human reads in the Editor window (FR-013).
    /// </summary>
    public static class GraphImportPipeline
    {
        public static ConflictReport Run(
            MappingConfig mapping,
            IReadOnlyDictionary<string, SourceTable> sourceTables,
            IPathTemplateResolver pathResolver,
            IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> generators)
        {
            mapping.Validate(sourceTables);

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);
            var plan = new PlanBuilder(pathResolver).Build(quests);
            var report = PlanConflictDetector.Detect(plan);
            PlanApplier.Apply(plan, report, generators);

            return report;
        }
    }
}
