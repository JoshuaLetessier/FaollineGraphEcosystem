using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Resolves a raw reference value against each declared target table's stable ID and/or
    /// declared fallback name column. A value that matches the same underlying row through more
    /// than one path (e.g. both by ID and by name) still counts as a single match.
    /// </summary>
    public sealed class IdOrNameReferenceResolver : IReferenceResolver
    {
        public PivotReference Resolve(SourceRow fromRow, ReferenceMapping reference,
            IReadOnlyDictionary<string, SourceTable> sourceTables,
            IReadOnlyDictionary<string, TableMapping> tableMappingsByName)
        {
            if (!fromRow.Values.TryGetValue(reference.SourceColumn, out var raw) || string.IsNullOrWhiteSpace(raw))
                return null;

            var candidates = new List<SourceRow>();
            var seen = new HashSet<(string table, string id)>();

            foreach (var targetTableName in reference.TargetTables)
            {
                if (!sourceTables.TryGetValue(targetTableName, out var targetTable))
                    continue;
                tableMappingsByName.TryGetValue(targetTableName, out var targetMapping);

                foreach (var key in reference.MatchOn)
                {
                    var column = key.IsId ? targetMapping?.IdColumn : key.NameColumn;
                    if (column == null)
                        continue;

                    foreach (var candidateRow in targetTable.Rows)
                    {
                        if (!candidateRow.Values.TryGetValue(column, out var candidateValue) || candidateValue != raw)
                            continue;

                        var canonicalId = ResolveCanonicalId(candidateRow, targetMapping, raw);
                        if (seen.Add((targetTableName, canonicalId)))
                            candidates.Add(candidateRow);
                    }
                }
            }

            if (candidates.Count == 0)
                throw new ReferenceResolutionException(fromRow.Table, fromRow.RowIndex, reference.SourceColumn, raw, ReferenceResolutionReason.NotFound, candidates);
            if (candidates.Count > 1)
                throw new ReferenceResolutionException(fromRow.Table, fromRow.RowIndex, reference.SourceColumn, raw, ReferenceResolutionReason.Ambiguous, candidates);

            var resolvedRow = candidates[0];
            tableMappingsByName.TryGetValue(resolvedRow.Table.Name, out var resolvedMapping);
            return new PivotReference(resolvedRow.Table.Name, ResolveCanonicalId(resolvedRow, resolvedMapping, raw));
        }

        static string ResolveCanonicalId(SourceRow row, TableMapping mapping, string fallback) =>
            mapping != null && row.Values.TryGetValue(mapping.IdColumn, out var id) ? id : fallback;
    }
}
