using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>Raw parsed rows from one input file, before any mapping is applied.</summary>
    public sealed class SourceTable
    {
        readonly List<SourceRow> _rows = new List<SourceRow>();

        public string Name { get; }
        public IReadOnlyList<string> Header { get; }
        public IReadOnlyList<SourceRow> Rows => _rows;

        public SourceTable(string name, IReadOnlyList<string> header)
        {
            Name = name;
            Header = header;
        }

        /// <summary>
        /// Appends a row owned by this table. RowIndex is 1-based and assigned in append order,
        /// so a source's error messages can point back at the row as the user would count it.
        /// </summary>
        public SourceRow AddRow(IReadOnlyDictionary<string, string> values)
        {
            var row = new SourceRow(this, _rows.Count + 1, values);
            _rows.Add(row);
            return row;
        }
    }

    /// <summary>One row of a <see cref="SourceTable"/>: raw column name to raw string value.</summary>
    public sealed class SourceRow
    {
        public SourceTable Table { get; }
        public int RowIndex { get; }
        public IReadOnlyDictionary<string, string> Values { get; }

        internal SourceRow(SourceTable table, int rowIndex, IReadOnlyDictionary<string, string> values)
        {
            Table = table;
            RowIndex = rowIndex;
            Values = values;
        }
    }
}
