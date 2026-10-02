using System;
using System.Collections.Generic;
using Faolline.GraphLocalization;
using UnityEditor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Backs the Speaker inspector's localization-group dropdown: the groups already used by speakers in the project,
    /// offered as "(None)", each group, then "New group…" — so a group is typed once, then picked, and a typo can't
    /// silently create a stray table.
    /// </summary>
    public static class SpeakerGroupCatalog
    {
        public const string NoneLabel = "(None)";
        public const string NewGroupLabel = "New group…";

        /// <summary>The dropdown's entries for one current value.</summary>
        public sealed class PopupModel
        {
            /// <summary>Displayed entries: "(None)", the groups, "New group…".</summary>
            public string[] Labels { get; }

            /// <summary>The group each entry sets: empty for "(None)", null for "New group…".</summary>
            public string[] Values { get; }

            /// <summary>Index of the current value.</summary>
            public int Selected { get; }

            /// <summary>Index of "New group…" (always the last entry).</summary>
            public int NewGroupIndex => Labels.Length - 1;

            internal PopupModel(string[] labels, string[] values, int selected)
            {
                Labels = labels;
                Values = values;
                Selected = selected;
            }
        }

        /// <summary>
        /// Distinct groups used by the project's <see cref="Speaker"/> assets, trimmed, without the empty group.
        /// Spellings differing only by letter case count once (the first by asset path wins, like the table build).
        /// Sorted case-insensitively.
        /// </summary>
        public static IReadOnlyList<string> Collect()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Speaker"))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            var byIdentity = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var speaker = AssetDatabase.LoadAssetAtPath<Speaker>(path);
                var group = LocalizationTableNames.NormalizeGroup(speaker != null ? speaker.LocalizationGroup : null);
                if (group == null) continue;
                var identity = LocalizationTableNames.GroupComparisonKey(group);
                if (!byIdentity.ContainsKey(identity)) byIdentity[identity] = group;
            }

            var groups = new List<string>(byIdentity.Values);
            groups.Sort(StringComparer.OrdinalIgnoreCase);
            return groups;
        }

        /// <summary>
        /// Builds the dropdown for <paramref name="current"/>: a blank value selects "(None)"; a value matching a
        /// group (ignoring case and surrounding spaces) selects it; any other value is kept as its own entry, so
        /// opening the dropdown never silently changes it.
        /// </summary>
        public static PopupModel BuildPopup(IReadOnlyList<string> groups, string current)
        {
            var values = new List<string> { string.Empty };
            if (groups != null) values.AddRange(groups);

            int selected = 0;
            var normalized = LocalizationTableNames.NormalizeGroup(current);
            if (normalized != null)
            {
                var identity = LocalizationTableNames.GroupComparisonKey(normalized);
                selected = values.FindIndex(1, v => LocalizationTableNames.GroupComparisonKey(v) == identity);
                if (selected < 0)
                {
                    values.Add(normalized);
                    selected = values.Count - 1;
                }
            }

            var labels = new List<string>(values.Count + 1) { NoneLabel };
            for (int i = 1; i < values.Count; i++) labels.Add(values[i]);
            labels.Add(NewGroupLabel);
            values.Add(null);

            return new PopupModel(labels.ToArray(), values.ToArray(), selected);
        }
    }
}
