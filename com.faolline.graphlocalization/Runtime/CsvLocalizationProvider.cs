using System.Collections.Generic;
using System.Text;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Default, self-contained <see cref="ILocalizationProvider"/> with no external dependency.
    /// Parses a simple CSV whose first column is the key and remaining headers are locale codes
    /// (e.g. <c>Key,en,fr</c>). RFC4180: supports quoted fields with embedded commas, doubled
    /// quotes, and embedded newlines (multi-line text round-trips through the exporter intact).
    /// </summary>
    public sealed class CsvLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<string, Dictionary<string, string>> _table = new();
        private string _currentLocale;

        public string CurrentLocale => _currentLocale;

        public CsvLocalizationProvider(string csvText, string currentLocale = null)
        {
            var locales = Parse(csvText);
            if (!string.IsNullOrEmpty(currentLocale)) _currentLocale = currentLocale;
            else if (locales.Count > 0) _currentLocale = locales[0];
            else _currentLocale = "en";
        }

        public void SetLocale(string locale) { if (!string.IsNullOrEmpty(locale)) _currentLocale = locale; }

        /// <summary>
        /// Merges additional CSV content into this provider (later files override earlier values for the
        /// same key+locale). Lets one provider serve keys spread across several per-graph CSV files.
        /// </summary>
        public void Append(string csvText) => Parse(csvText);

        // A missing key returns the "#key" marker SILENTLY: the marker is the signal, and how loudly to react
        // belongs to the layer that knows the LocalizationStrictMode (LocalizationSettings.Resolve, or a
        // consumer like DialoguePresenter) — Permissive there means genuinely silent, and Audit already
        // warns once per key without this provider double-logging on every lookup.
        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_table.TryGetValue(key, out var byLocale) &&
                byLocale.TryGetValue(locale ?? _currentLocale, out var text) &&
                !string.IsNullOrEmpty(text))
                return text;

            return $"#{key}";
        }

        private List<string> Parse(string csvText)
        {
            var locales = new List<string>();
            var records = ParseRecords(csvText);
            if (records.Count == 0) return locales;

            var header = records[0];
            if (header.Count < 2) return locales;
            for (int c = 1; c < header.Count; c++) locales.Add(header[c].Trim());

            for (int i = 1; i < records.Count; i++)
            {
                var cols = records[i];
                if (cols.Count == 0) continue;
                var key = cols[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;
                if (!_table.TryGetValue(key, out var byLocale)) { byLocale = new(); _table[key] = byLocale; }
                for (int c = 1; c < header.Count && c < cols.Count; c++) byLocale[locales[c - 1]] = cols[c];
            }
            return locales;
        }

        // Full-text RFC4180 tokenizer. Unlike a Split('\n')-then-parse approach, a quoted field may contain
        // commas, doubled quotes AND newlines — the newline case is what lets multi-line text written by
        // CsvLocalizationExporter.Escape (or a translator's spreadsheet) round-trip intact.
        // Kept in sync with the identical copy in CsvLocalizationExporter (editor assembly).
        private static List<List<string>> ParseRecords(string csvText)
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
    }
}
