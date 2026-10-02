using System;
using System.Collections.Generic;
using System.Text;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Speaker → localization table group, read from a project-produced CSV with a <c>SpeakerKey</c> and a
    /// <c>Table</c> column (any order, other columns ignored). The dialogue import uses it to give each speaker the
    /// group its display name is built into (e.g. one table per chapter). Validated as a whole by
    /// <see cref="Parse"/> — a malformed mapping is rejected before anything is written, never guessed.
    /// </summary>
    public sealed class SpeakerGroupMapping
    {
        private const string KeyColumn = "SpeakerKey";
        private const string GroupColumn = "Table";

        private readonly Dictionary<string, string> _groupByKey;

        private SpeakerGroupMapping(Dictionary<string, string> groupByKey) => _groupByKey = groupByKey;

        /// <summary>Number of distinct speaker keys listed.</summary>
        public int Count => _groupByKey.Count;

        /// <summary>
        /// True when <paramref name="speakerKey"/> is listed; <paramref name="group"/> is its trimmed group, empty
        /// when the row explicitly gives no group (the default speakers table).
        /// </summary>
        public bool TryGetGroup(string speakerKey, out string group)
        {
            group = null;
            return speakerKey != null && _groupByKey.TryGetValue(speakerKey.Trim(), out group);
        }

        /// <summary>
        /// Parses the mapping (RFC4180; values trimmed). Throws <see cref="SpeakerGroupMappingException"/> when a
        /// required column is missing, a row has an empty key, or one key is listed with two different groups (an
        /// exact duplicate row is accepted).
        /// </summary>
        public static SpeakerGroupMapping Parse(string csvText)
        {
            var records = ReadRecords(csvText ?? string.Empty);
            var header = records.Count > 0 ? records[0] : new List<string>();
            int keyIndex = IndexOf(header, KeyColumn);
            int groupIndex = IndexOf(header, GroupColumn);
            if (keyIndex < 0) throw new SpeakerGroupMappingException($"Speaker tables CSV has no '{KeyColumn}' column.");
            if (groupIndex < 0) throw new SpeakerGroupMappingException($"Speaker tables CSV has no '{GroupColumn}' column.");

            var groupByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < records.Count; i++)
            {
                var row = records[i];
                var key = Cell(row, keyIndex);
                var group = Cell(row, groupIndex);
                if (key.Length == 0)
                    throw new SpeakerGroupMappingException($"Speaker tables CSV line {i + 1}: empty '{KeyColumn}'.");

                if (groupByKey.TryGetValue(key, out var existing))
                {
                    if (existing != group)
                        throw new SpeakerGroupMappingException(
                            $"Speaker tables CSV lists '{key}' twice with different groups: '{existing}' and '{group}'.");
                    continue;
                }
                groupByKey[key] = group;
            }
            return new SpeakerGroupMapping(groupByKey);
        }

        private static int IndexOf(List<string> header, string column)
        {
            for (int i = 0; i < header.Count; i++)
                if (header[i].Trim() == column) return i;
            return -1;
        }

        private static string Cell(List<string> row, int index) => index < row.Count ? row[index].Trim() : string.Empty;

        // Minimal RFC4180 reader (quoted fields may hold commas, doubled quotes and line breaks). This assembly is
        // noEngineReferences and cannot use graphlocalization's LocalizationCsv, so it carries its own small copy.
        private static List<List<string>> ReadRecords(string text)
        {
            var records = new List<List<string>>();
            var row = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            void EndCell() { row.Add(sb.ToString()); sb.Clear(); }
            void EndRecord()
            {
                EndCell();
                if (row.Count > 1 || row[0].Trim().Length > 0) records.Add(new List<string>(row));
                row.Clear();
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = false; }
                    else sb.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') EndCell();
                else if (ch == '\r') { if (i + 1 >= text.Length || text[i + 1] != '\n') EndRecord(); }
                else if (ch == '\n') EndRecord();
                else sb.Append(ch);
            }
            if (sb.Length > 0 || row.Count > 0) EndRecord();
            return records;
        }
    }
}
