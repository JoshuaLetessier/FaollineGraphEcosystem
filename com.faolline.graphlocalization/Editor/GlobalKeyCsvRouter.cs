using System;
using System.Collections.Generic;
using System.Text;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Splits one translation CSV of global keys — e.g. dialogue-studio's single <c>speakers.csv</c>, while speaker
    /// names live in one table per group — into one CSV per collection, routing each row to the collection that
    /// holds its key. Never guesses: a key held by no collection, or by several, is reported as a failure and its
    /// row is placed nowhere; the other rows are still routed. Pure (no Unity Localization dependency).
    /// </summary>
    public static class GlobalKeyCsvRouter
    {
        /// <summary>The routed CSVs and the rows that could not be routed.</summary>
        public sealed class Result
        {
            /// <summary>Collection name → CSV text (the original header + that collection's rows).</summary>
            public IReadOnlyDictionary<string, string> CsvByCollection { get; }

            /// <summary>One message per row that was placed nowhere, naming its key.</summary>
            public IReadOnlyList<string> Failures { get; }

            internal Result(IReadOnlyDictionary<string, string> csvByCollection, IReadOnlyList<string> failures)
            {
                CsvByCollection = csvByCollection;
                Failures = failures;
            }
        }

        /// <summary>
        /// Routes every data row of <paramref name="csvText"/> (first record = header, first column = key) to the
        /// single collection <paramref name="collectionsHoldingKey"/> returns for its key.
        /// </summary>
        public static Result Route(string csvText, Func<string, IReadOnlyList<string>> collectionsHoldingKey)
        {
            var buckets = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
            var failures = new List<string>();
            var records = LocalizationCsv.ParseRecords(csvText);
            if (records.Count < 2) return new Result(new Dictionary<string, string>(), failures);

            var header = Line(records[0]);
            for (int i = 1; i < records.Count; i++)
            {
                var row = records[i];
                var key = row.Count > 0 ? row[0].Trim() : string.Empty;
                if (key.Length == 0) continue;

                var holders = collectionsHoldingKey(key) ?? Array.Empty<string>();
                if (holders.Count == 0)
                {
                    failures.Add($"key '{key}' found in no collection — build the localization tables first.");
                    continue;
                }
                if (holders.Count > 1)
                {
                    failures.Add($"key '{key}' is ambiguous: held by {string.Join(", ", holders)}.");
                    continue;
                }

                if (!buckets.TryGetValue(holders[0], out var sb))
                    buckets[holders[0]] = sb = new StringBuilder(header);
                sb.Append(Line(row));
            }

            var csvByCollection = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in buckets) csvByCollection[kv.Key] = kv.Value.ToString();
            return new Result(csvByCollection, failures);
        }

        /// <summary>
        /// The locale code a CSV column header stands for: a bare code (<c>fr</c>, as written by dialogue-studio and
        /// this lib's exporter) or Unity Localization's own export name (<c>French(fr)</c>) — the code is the text
        /// inside the last parentheses.
        /// </summary>
        public static string LocaleCodeOfColumn(string column)
        {
            var trimmed = (column ?? string.Empty).Trim();
            if (trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                var open = trimmed.LastIndexOf('(');
                if (open >= 0 && open < trimmed.Length - 2)
                    return trimmed.Substring(open + 1, trimmed.Length - open - 2).Trim();
            }
            return trimmed;
        }

        private static string Line(List<string> cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(LocalizationCsv.Escape(cells[i]));
            }
            return sb.Append('\n').ToString();
        }
    }
}
