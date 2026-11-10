namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// A graph text that no longer matches the source-language cell of its table. The table is the reference
    /// (the runtime reads only the table; the build never overwrites a filled cell), so a drift means the graph
    /// shows an out-of-date copy — or was edited where the table should have been. Found by
    /// <see cref="GraphTextSync"/>; fixed, on request only, by copying <see cref="TableText"/> into the graph.
    /// </summary>
    public sealed class GraphTextDrift
    {
        /// <summary>The lib (adapter) the key belongs to, e.g. <c>GraphDialogue</c>.</summary>
        public string LibName { get; }

        /// <summary>Asset GUID of the graph holding the text; null for a global key (e.g. a speaker name).</summary>
        public string GraphGuid { get; }

        /// <summary>Name of the graph holding the text; null for a global key.</summary>
        public string GraphName { get; }

        /// <summary>Base name of the table the build files the key under (see <see cref="LocalizationTableNames"/>).</summary>
        public string Table { get; }

        /// <summary>The localization key.</summary>
        public string Key { get; }

        /// <summary>The kind of text (line, choice label, speaker name…).</summary>
        public LocalizationKeyType Type { get; }

        /// <summary>Id of the node holding the text, when the adapter reported one.</summary>
        public string NodeId { get; }

        /// <summary>The text currently in the graph (the adapter's <see cref="LocalizationKeyEntry.DefaultHint"/>).</summary>
        public string GraphText { get; }

        /// <summary>The text in the table's source-language cell, line endings normalized to <c>\n</c>.</summary>
        public string TableText { get; }

        /// <summary>True for a key not tied to a graph (no <see cref="GraphGuid"/>).</summary>
        public bool IsGlobal => string.IsNullOrEmpty(GraphGuid);

        public GraphTextDrift(string libName, string graphGuid, string graphName, string table, string key,
            LocalizationKeyType type, string nodeId, string graphText, string tableText)
        {
            LibName = libName ?? string.Empty;
            GraphGuid = string.IsNullOrEmpty(graphGuid) ? null : graphGuid;
            GraphName = string.IsNullOrEmpty(graphName) ? null : graphName;
            Table = table ?? string.Empty;
            Key = key ?? string.Empty;
            Type = type;
            NodeId = nodeId ?? string.Empty;
            GraphText = graphText ?? string.Empty;
            TableText = tableText ?? string.Empty;
        }

        /// <summary>Where the text lives: <c>{graph} / {key}</c>, or <c>{table} / {key}</c> for a global key.</summary>
        public string Location => $"{(IsGlobal ? Table : GraphName)} / {Key}";

        public override string ToString() => $"{Location}: graph \"{GraphText}\" ≠ table \"{TableText}\"";
    }
}
