using System.Collections.Generic;
using System.IO;

namespace Faolline.GraphImport
{
    /// <summary>RFC 4180 CSV reader: quoted fields, embedded commas/newlines, "" escaping.</summary>
    public sealed class CsvRowSource : IRowSource
    {
        public SourceTable Read(string filePath, string tableName)
        {
            var records = ParseRecords(File.ReadAllText(filePath));
            if (records.Count == 0)
                return new SourceTable(tableName, new List<string>());

            var header = records[0];
            var table = new SourceTable(tableName, header);

            for (var r = 1; r < records.Count; r++)
            {
                var record = records[r];
                var values = new Dictionary<string, string>();
                for (var c = 0; c < header.Count; c++)
                    values[header[c]] = c < record.Count ? record[c] : string.Empty;
                table.AddRow(values);
            }

            return table;
        }

        /// <summary>Splits raw CSV text into records of fields, per RFC 4180.</summary>
        static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new System.Text.StringBuilder();
            var inQuotes = false;
            var i = 0;
            var sawAnyFieldInRecord = false;

            void EndField()
            {
                record.Add(field.ToString());
                field.Clear();
                sawAnyFieldInRecord = true;
            }

            void EndRecord()
            {
                EndField();
                records.Add(record);
                record = new List<string>();
                sawAnyFieldInRecord = false;
            }

            while (i < text.Length)
            {
                var ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    field.Append(ch);
                    i++;
                    continue;
                }

                switch (ch)
                {
                    case '"':
                        inQuotes = true;
                        i++;
                        break;
                    case ',':
                        EndField();
                        i++;
                        break;
                    case '\r':
                        i++;
                        break;
                    case '\n':
                        EndRecord();
                        i++;
                        break;
                    default:
                        field.Append(ch);
                        i++;
                        break;
                }
            }

            if (field.Length > 0 || sawAnyFieldInRecord || record.Count > 0)
                EndRecord();

            return records;
        }
    }
}
