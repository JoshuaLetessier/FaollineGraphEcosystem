using System;
using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport
{
    /// <summary>Builds the pivot model from mapped, resolved source data.</summary>
    public sealed class PivotBuilder
    {
        readonly MappingConfig _mapping;
        readonly IReferenceResolver _resolver;
        readonly IBranchDetectionStrategy _branchStrategy;

        public PivotBuilder(MappingConfig mapping, IReferenceResolver resolver, IBranchDetectionStrategy branchStrategy = null)
        {
            _mapping = mapping;
            _resolver = resolver;
            _branchStrategy = branchStrategy ?? new DeclaredColumnBranchStrategy();
        }

        /// <summary>
        /// Builds one <see cref="PivotQuest"/> per row of every table declared with <see cref="TableRole.Quest"/>,
        /// then attaches steps/branches built from every table declared with <see cref="TableRole.Step"/>.
        /// </summary>
        public IReadOnlyList<PivotQuest> Build(IReadOnlyDictionary<string, SourceTable> sourceTables)
        {
            var tableMappingsByName = _mapping.TableMappingsByName;
            var quests = BuildQuests(sourceTables, tableMappingsByName);
            var questsById = quests.ToDictionary(q => q.Id);

            AttachSteps(sourceTables, tableMappingsByName, questsById);

            return quests;
        }

        List<PivotQuest> BuildQuests(IReadOnlyDictionary<string, SourceTable> sourceTables, IReadOnlyDictionary<string, TableMapping> tableMappingsByName)
        {
            var quests = new List<PivotQuest>();

            foreach (var table in _mapping.Tables.Where(t => t.Role == TableRole.Quest))
            {
                var sourceTable = sourceTables[table.SourceTableName];
                var nameField = table.Fields.FirstOrDefault(f => f.PivotField == "name");

                foreach (var row in sourceTable.Rows)
                {
                    var id = row.Values[table.IdColumn];
                    var name = nameField != null && row.Values.TryGetValue(nameField.SourceColumn, out var n) ? n : id;

                    var fields = table.Fields.ToDictionary(f => f.PivotField, f => row.Values.TryGetValue(f.SourceColumn, out var v) ? v : string.Empty);

                    var references = new Dictionary<string, IReadOnlyList<PivotReference>>();
                    foreach (var referenceMapping in table.References)
                    {
                        var resolved = _resolver.Resolve(row, referenceMapping, sourceTables, tableMappingsByName);
                        references[referenceMapping.PivotField] = resolved == null
                            ? Array.Empty<PivotReference>()
                            : new[] { resolved };
                    }

                    quests.Add(new PivotQuest(id, name, fields, references));
                }
            }

            return quests;
        }

        void AttachSteps(IReadOnlyDictionary<string, SourceTable> sourceTables, IReadOnlyDictionary<string, TableMapping> tableMappingsByName,
            IReadOnlyDictionary<string, PivotQuest> questsById)
        {
            var stepsByQuestId = new Dictionary<string, List<PivotStep>>();

            foreach (var table in _mapping.Tables.Where(t => t.Role == TableRole.Step))
            {
                var sourceTable = sourceTables[table.SourceTableName];
                var orderField = table.Fields.FirstOrDefault(f => f.PivotField == "order")
                    ?? throw new InvalidOperationException($"Step table '{table.SourceTableName}' has no field mapped to pivot field 'order'.");
                var branchOutcomeField = table.Fields.FirstOrDefault(f => f.PivotField == "branchOutcome");
                var questReference = table.References.FirstOrDefault(r => r.PivotField == "quest")
                    ?? throw new InvalidOperationException($"Step table '{table.SourceTableName}' has no reference mapped to pivot field 'quest'.");
                var contentReference = table.References.FirstOrDefault(r => r.PivotField == "content")
                    ?? throw new InvalidOperationException($"Step table '{table.SourceTableName}' has no reference mapped to pivot field 'content'.");

                foreach (var row in sourceTable.Rows)
                {
                    var questRef = _resolver.Resolve(row, questReference, sourceTables, tableMappingsByName)
                        ?? throw new InvalidOperationException($"Step '{table.SourceTableName}' row {row.RowIndex} has no resolvable quest reference.");
                    var quest = questsById[questRef.TargetId];

                    var contentRef = _resolver.Resolve(row, contentReference, sourceTables, tableMappingsByName);

                    var order = int.Parse(row.Values[orderField.SourceColumn]);
                    var branchOutcome = branchOutcomeField != null
                        && row.Values.TryGetValue(branchOutcomeField.SourceColumn, out var outcomeValue)
                        && !string.IsNullOrWhiteSpace(outcomeValue)
                        ? outcomeValue
                        : null;

                    var step = new PivotStep(row.Values[table.IdColumn], quest, order, contentRef, branchOutcome);

                    if (!stepsByQuestId.TryGetValue(quest.Id, out var list))
                        stepsByQuestId[quest.Id] = list = new List<PivotStep>();
                    list.Add(step);
                }
            }

            foreach (var (questId, steps) in stepsByQuestId)
            {
                var quest = questsById[questId];
                var (linear, branches) = _branchStrategy.Detect(quest, steps);
                quest.Steps = linear.Concat(branches.SelectMany(b => b.Steps)).OrderBy(s => s.Order).ToList();
                quest.Branches = branches;
            }
        }
    }
}
