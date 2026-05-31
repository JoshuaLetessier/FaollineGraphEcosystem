using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Default, self-contained <see cref="ILocalizationProvider"/> with no external dependency.
    /// Loads a simple CSV table whose first column is the key and whose remaining header columns are
    /// locale codes (e.g. <c>Key,en,fr</c>). Resolution returns the cell for the requested locale, or
    /// a <c>#key</c> fallback (with a warning) when the key/locale is missing. RFC4180-light parsing:
    /// supports quoted fields, embedded commas, and doubled quotes.
    /// </summary>
    public sealed class CsvLocalizationProvider : ILocalizationProvider
    {
        // key -> (locale -> text)
        private readonly Dictionary<string, Dictionary<string, string>> _table =
            new Dictionary<string, Dictionary<string, string>>();

        private string _currentLocale;

        /// <inheritdoc/>
        public string CurrentLocale => _currentLocale;

        /// <summary>
        /// Builds the provider from CSV text. <paramref name="currentLocale"/> is the initial active
        /// locale; if null/empty, the first locale column (if any) is used, else "en".
        /// </summary>
        public CsvLocalizationProvider(string csvText, string currentLocale = null)
        {
            var locales = Parse(csvText);
            if (!string.IsNullOrEmpty(currentLocale))
                _currentLocale = currentLocale;
            else if (locales.Count > 0)
                _currentLocale = locales[0];
            else
                _currentLocale = "en";
        }

        /// <summary>Sets the active locale used by single-argument resolution helpers.</summary>
        public void SetLocale(string locale)
        {
            if (!string.IsNullOrEmpty(locale))
                _currentLocale = locale;
        }

        /// <inheritdoc/>
        public string Resolve(string key, string locale)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_table.TryGetValue(key, out var byLocale) &&
                byLocale.TryGetValue(locale ?? _currentLocale, out var text) &&
                !string.IsNullOrEmpty(text))
            {
                return text;
            }

            Debug.LogWarning(
                $"[GraphDialogue] Localization key '{key}' not found for locale '{locale ?? _currentLocale}' — using fallback.");
            return $"#{key}";
        }

        // ── CSV parsing ────────────────────────────────────────────────────────

        // Returns the ordered list of locale column codes discovered in the header.
        private List<string> Parse(string csvText)
        {
            var locales = new List<string>();
            if (string.IsNullOrEmpty(csvText)) return locales;

            var lines = SplitLines(csvText);
            if (lines.Count == 0) return locales;

            var header = ParseLine(lines[0]);
            if (header.Count < 2) return locales; // need at least Key + one locale

            for (int c = 1; c < header.Count; c++)
                locales.Add(header[c].Trim());

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = ParseLine(lines[i]);
                if (cols.Count == 0) continue;

                var key = cols[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!_table.TryGetValue(key, out var byLocale))
                {
                    byLocale = new Dictionary<string, string>();
                    _table[key] = byLocale;
                }

                for (int c = 1; c < header.Count && c < cols.Count; c++)
                    byLocale[locales[c - 1]] = cols[c];
            }

            return locales;
        }

        private static List<string> SplitLines(string text)
        {
            // Normalize CRLF/CR to LF, then split — but keep newlines inside quotes intact by
            // splitting on a per-line parse is overkill here; the table cells in this MVP do not
            // contain embedded newlines, so a simple normalized split is sufficient and deterministic.
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var raw = normalized.Split('\n');
            var result = new List<string>(raw.Length);
            foreach (var line in raw)
                result.Add(line);
            return result;
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
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
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
