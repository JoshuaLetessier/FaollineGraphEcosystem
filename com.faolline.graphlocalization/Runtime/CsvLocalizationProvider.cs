using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Default, self-contained <see cref="ILocalizationProvider"/> with no external dependency.
    /// Parses a simple CSV whose first column is the key and remaining headers are locale codes
    /// (e.g. <c>Key,en,fr</c>). RFC4180-light: supports quoted fields, embedded commas, doubled quotes.
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

        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_table.TryGetValue(key, out var byLocale) &&
                byLocale.TryGetValue(locale ?? _currentLocale, out var text) &&
                !string.IsNullOrEmpty(text))
                return text;

            Debug.LogWarning($"[GraphLocalization] Key '{key}' not found for locale '{locale ?? _currentLocale}'.");
            return $"#{key}";
        }

        private List<string> Parse(string csvText)
        {
            var locales = new List<string>();
            if (string.IsNullOrEmpty(csvText)) return locales;

            var lines = csvText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            if (lines.Length == 0) return locales;

            var header = ParseLine(lines[0]);
            if (header.Count < 2) return locales;
            for (int c = 1; c < header.Count; c++) locales.Add(header[c].Trim());

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = ParseLine(lines[i]);
                if (cols.Count == 0) continue;
                var key = cols[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;
                if (!_table.TryGetValue(key, out var byLocale)) { byLocale = new(); _table[key] = byLocale; }
                for (int c = 1; c < header.Count && c < cols.Count; c++) byLocale[locales[c - 1]] = cols[c];
            }
            return locales;
        }

        private static List<string> ParseLine(string line)
        {
            var result = new List<string>();
            if (line == null) { result.Add(string.Empty); return result; }
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = false; }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else if (ch == '"') inQuotes = true;
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}
