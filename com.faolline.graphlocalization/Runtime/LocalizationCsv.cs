using System.Collections.Generic;
using System.Text;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Shared RFC4180 CSV helpers for every localization CSV this lib reads or writes (the runtime CSV
    /// provider, the table exporter, the translation import router). One implementation, so multi-line
    /// text written by <see cref="Escape"/> always parses back intact through <see cref="ParseRecords"/>.
    /// </summary>
    public static class LocalizationCsv
    {
        /// <summary>
        /// Full-text RFC4180 tokenizer: a quoted field may contain commas, doubled quotes AND newlines.
        /// <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> end a record; blank/whitespace-only lines are skipped.
        /// Returns an empty list for null/empty input.
        /// </summary>
        public static List<List<string>> ParseRecords(string csvText)
        {
            var records = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText)) return records;

            var row = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            void EndCell() { row.Add(sb.ToString()); sb.Clear(); }
            void EndRecord()
            {
                EndCell();
                // A blank/whitespace-only line parses as a single blank cell — skip it.
                if (row.Count > 1 || row[0].Trim().Length > 0)
                    records.Add(new List<string>(row));
                row.Clear();
            }

            for (int i = 0; i < csvText.Length; i++)
            {
                char ch = csvText[i];
                if (inQuotes)
                {
                    if (ch == '"') { if (i + 1 < csvText.Length && csvText[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = false; }
                    else sb.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') EndCell();
                else if (ch == '\r') { if (i + 1 >= csvText.Length || csvText[i + 1] != '\n') EndRecord(); }   // lone \r ends the record; \r\n defers to the \n
                else if (ch == '\n') EndRecord();
                else sb.Append(ch);
            }
            if (sb.Length > 0 || row.Count > 0) EndRecord();
            return records;
        }

        /// <summary>Quotes a field when it contains a comma, a quote or a line break (quotes doubled). Null gives empty.</summary>
        public static string Escape(string field)
        {
            field ??= string.Empty;
            if (field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}
