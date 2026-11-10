using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Generic base for per-lib localization adapters. Handles the <see cref="AssetDatabase"/> scan
    /// and metadata bookkeeping — subclasses provide only key extraction, and optionally the graph-text
    /// write-back (<see cref="ApplyGraphText"/> / <see cref="ApplyGlobalText"/>, see <see cref="GraphTextSync"/>).
    /// <typeparam name="TGraph">The ScriptableObject graph type to scan (e.g. DialogueGraph, QuestGraph).</typeparam>
    /// </summary>
    public abstract class BaseGraphLocalizationAdapter<TGraph> : IGraphLocalizationAdapter, IGraphTextWriter
        where TGraph : ScriptableObject
    {
        public abstract string LibName { get; }

        public void ScanAndIndex(LocalizationDatabase database)
        {
            int totalKeys = 0;

            var guids = AssetDatabase.FindAssets($"t:{typeof(TGraph).Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<TGraph>(path);
                if (graph == null) continue;

                var entry = database.GetOrCreateGraphEntry(guid, graph.name);
                totalKeys += ExtractGraphKeys(graph, entry);
            }

            int globalKeys = ExtractGlobalKeys(database);
            totalKeys += globalKeys;

            database.TotalGraphsScanned = guids.Length;
            database.TotalKeysFound = totalKeys;
        }

        /// <summary>
        /// Extract localization keys from a single graph asset and add them to <paramref name="entry"/>.
        /// Return the number of keys added.
        /// </summary>
        protected abstract int ExtractGraphKeys(TGraph graph, LocalizationGraphEntry entry);

        /// <summary>
        /// Extract global keys not tied to a specific graph (e.g. Speaker display names).
        /// Override to add lib-specific global keys. Default returns 0.
        /// </summary>
        protected virtual int ExtractGlobalKeys(LocalizationDatabase database) => 0;

        /// <summary>Undo label recorded by the graph-text write-back.</summary>
        public const string WriteGraphTextUndoName = "Update Graph Text From Table";

        /// <summary>
        /// Copies <see cref="GraphTextDrift.TableText"/> back into the graph text it was seeded from (054): a global
        /// drift goes to <see cref="ApplyGlobalText"/>; a graph drift loads the graph asset by
        /// <see cref="GraphTextDrift.GraphGuid"/> and goes to <see cref="WriteGraphText(TGraph, GraphTextDrift)"/>.
        /// Does not save. False when nothing was written.
        /// </summary>
        public bool WriteGraphText(GraphTextDrift drift)
        {
            if (drift == null) return false;
            if (drift.IsGlobal) return ApplyGlobalText(drift);

            var path = AssetDatabase.GUIDToAssetPath(drift.GraphGuid);
            var graph = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TGraph>(path);
            return graph != null && WriteGraphText(graph, drift);
        }

        /// <summary>
        /// Same as <see cref="WriteGraphText(GraphTextDrift)"/> on a graph you already hold (an asset or an in-memory
        /// graph): records Undo, calls <see cref="ApplyGraphText"/>, marks the graph dirty when it wrote. Does not save.
        /// </summary>
        public bool WriteGraphText(TGraph graph, GraphTextDrift drift)
        {
            if (graph == null || drift == null) return false;
            Undo.RecordObject(graph, WriteGraphTextUndoName);
            if (!ApplyGraphText(graph, drift)) return false;
            EditorUtility.SetDirty(graph);
            return true;
        }

        /// <summary>
        /// Write <see cref="GraphTextDrift.TableText"/> into the text of <paramref name="graph"/> that produced
        /// <see cref="GraphTextDrift.Key"/> (the one passed as its <c>defaultHint</c>). Return false when the key is
        /// not found. Undo and dirtying are handled by the caller. Default: writes nothing.
        /// </summary>
        protected virtual bool ApplyGraphText(TGraph graph, GraphTextDrift drift) => false;

        /// <summary>
        /// Same as <see cref="ApplyGraphText"/> for a global key (added in <see cref="ExtractGlobalKeys"/>). Record
        /// Undo and dirty the assets you change yourself. Default: writes nothing.
        /// </summary>
        protected virtual bool ApplyGlobalText(GraphTextDrift drift) => false;
    }
}
