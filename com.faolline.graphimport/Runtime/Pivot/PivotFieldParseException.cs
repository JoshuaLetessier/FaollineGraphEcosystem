using System;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Raised when a mapped field's raw value can't be parsed into the type the pivot needs (e.g. an
    /// "order" column). Carries the same table/row/column context as
    /// <see cref="ReferenceResolutionException"/> — a bare <see cref="FormatException"/> gives no way
    /// to find the offending row in a multi-hundred-row export.
    /// </summary>
    public sealed class PivotFieldParseException : Exception
    {
        public SourceTable SourceTable { get; }
        public int SourceRowIndex { get; }
        public string SourceColumn { get; }
        public string PivotField { get; }
        public string RawValue { get; }

        public PivotFieldParseException(SourceTable sourceTable, int sourceRowIndex, string sourceColumn, string pivotField, string rawValue)
            : base($"{sourceTable.Name} row {sourceRowIndex}, column '{sourceColumn}': value '{rawValue}' is not a valid value for pivot field '{pivotField}'.")
        {
            SourceTable = sourceTable;
            SourceRowIndex = sourceRowIndex;
            SourceColumn = sourceColumn;
            PivotField = pivotField;
            RawValue = rawValue;
        }
    }
}
