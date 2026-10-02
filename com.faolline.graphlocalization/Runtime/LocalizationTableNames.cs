using System.Collections.Generic;
using System.Text;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Single source of every localization table name, shared by the table build (Unity syncer, CSV
    /// exporter), the translation import and the runtime lookup — so a name computed at runtime always
    /// matches the one the build produced.
    /// <para>
    /// A <b>table</b> is identified by its base name: <see cref="ForGraph"/> for a per-graph table,
    /// <see cref="ForGroup"/> for a group of keys not tied to one graph (e.g. speaker names). Physical
    /// artifacts derive from it: <see cref="TextCollection"/>, <see cref="AssetCollection"/>, or
    /// <c>{base}.csv</c>.
    /// </para>
    /// <para>
    /// Platform-stable on purpose: <see cref="Sanitize"/> uses a fixed character set (the Windows invalid
    /// file-name set) instead of <c>Path.GetInvalidFileNameChars()</c>, whose result differs per platform —
    /// a player on Android/iOS would otherwise compute a different name than the Windows editor that built
    /// the table.
    /// </para>
    /// </summary>
    public static class LocalizationTableNames
    {
        private const string DefaultGroup = "Global";
        private const string InvalidChars = "\"<>|:*?\\/";

        private static readonly (int flag, string name)[] s_assetTypes =
        {
            (1 << 1, "Audio"),
            (1 << 2, "Sprite"),
            (1 << 3, "Texture"),
            (1 << 4, "Video"),
            (1 << 5, "Font"),
        };

        /// <summary>
        /// Localized-asset table kinds, as (LocalizedAssetFlags bit, collection suffix). An asset table exists
        /// per kind only when at least one key carries that flag.
        /// </summary>
        public static IReadOnlyList<(int flag, string name)> AssetTypes => s_assetTypes;

        /// <summary>
        /// Replaces every character invalid in a file/collection name (<c>" &lt; &gt; | : * ? \ /</c> and
        /// control characters) with <c>_</c>. Null or empty gives <c>Unnamed</c>.
        /// </summary>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(c < 32 || InvalidChars.IndexOf(c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        /// <summary>Trims a group name; empty or whitespace-only means "no group" (null).</summary>
        public static string NormalizeGroup(string group)
        {
            if (group == null) return null;
            var trimmed = group.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>
        /// Identity of a group for collision checks: two groups with the same key would produce the same
        /// table on a case-insensitive file system (case-only difference, or names that sanitize alike).
        /// </summary>
        public static string GroupComparisonKey(string group)
        {
            var normalized = NormalizeGroup(group);
            return normalized == null ? string.Empty : Sanitize(normalized).ToLowerInvariant();
        }

        /// <summary>Base name of a per-graph table (dialogue, quest): the sanitized graph name.</summary>
        public static string ForGraph(string graphName) => Sanitize(graphName);

        /// <summary>
        /// Base name of a lib's table for keys not tied to one graph: <c>{lib}_{group}</c>, with
        /// <c>Global</c> as the default group. Lib-scoped, so two libs never share a table by accident.
        /// </summary>
        public static string ForGroup(string libName, string group)
            => Sanitize(libName) + "_" + Sanitize(NormalizeGroup(group) ?? DefaultGroup);

        /// <summary>Unity String Table Collection name for a table: <c>{table}_Text</c>.</summary>
        public static string TextCollection(string table) => table + "_Text";

        /// <summary>Unity Asset Table Collection name for a table and asset kind: <c>{table}_{assetType}</c>.</summary>
        public static string AssetCollection(string table, string assetType) => table + "_" + assetType;
    }
}
