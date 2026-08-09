using System;
using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport
{
    public enum ReferenceResolutionReason
    {
        NotFound,
        Ambiguous
    }

    /// <summary>
    /// Raised whenever a reference resolves to zero or more than one candidate row.
    /// Never swallowed — resolution never guesses (FR-003).
    /// </summary>
    public sealed class ReferenceResolutionException : Exception
    {
        public SourceTable SourceTable { get; }
        public int SourceRowIndex { get; }
        public string SourceColumn { get; }
        public string RawValue { get; }
        public ReferenceResolutionReason Reason { get; }
        public IReadOnlyList<SourceRow> CandidateRows { get; }

        public ReferenceResolutionException(SourceTable sourceTable, int sourceRowIndex, string sourceColumn,
            string rawValue, ReferenceResolutionReason reason, IReadOnlyList<SourceRow> candidateRows)
            : base(BuildMessage(sourceTable, sourceRowIndex, sourceColumn, rawValue, reason, candidateRows))
        {
            SourceTable = sourceTable;
            SourceRowIndex = sourceRowIndex;
            SourceColumn = sourceColumn;
            RawValue = rawValue;
            Reason = reason;
            CandidateRows = candidateRows;
        }

        static string BuildMessage(SourceTable sourceTable, int sourceRowIndex, string sourceColumn,
            string rawValue, ReferenceResolutionReason reason, IReadOnlyList<SourceRow> candidateRows)
        {
            var location = $"{sourceTable.Name} row {sourceRowIndex}, column '{sourceColumn}'";
            return reason == ReferenceResolutionReason.NotFound
                ? $"{location}: reference value '{rawValue}' did not resolve to any row."
                : $"{location}: reference value '{rawValue}' is ambiguous — matched {candidateRows.Count} rows across {string.Join(", ", candidateRows.Select(r => r.Table.Name))}.";
        }
    }
}
