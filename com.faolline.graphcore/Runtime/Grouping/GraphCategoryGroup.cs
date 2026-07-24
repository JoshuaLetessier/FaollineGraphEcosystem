using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named, creatable-with-no-code bucket of <see cref="BaseGraph"/> assets (e.g. "Main" / "Side"
    /// quests, dialogue acts, ...). Pure editor-time organizational metadata: nothing in the runtime
    /// (evaluators, conditions, the runner) reads this type, so unlike <c>VariableDef</c>/<c>SignalDef</c>/
    /// <see cref="Collections.CollectionDef"/> it carries no stable-GUID identity — there is no runtime
    /// lookup key to keep stable across a rename.
    /// <para>
    /// A graph may belong to any number of groups at once (zero, one, or several) — this is intentional,
    /// not a gap to close. Membership is forward-only (group → graphs); consumers that need "which
    /// group(s) contain graph X" scan project <see cref="GraphCategoryGroup"/> assets for it (see
    /// <c>GraphCategoryGroupInspectorExtension</c> in the Editor assembly).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Graph Category Group", fileName = "NewGraphCategoryGroup")]
    public class GraphCategoryGroup : ScriptableObject
    {
        [SerializeField, Tooltip("Display label for this group (e.g. \"Main\", \"Side\"). Falls back to the asset name when empty.")]
        private string _label;

        [SerializeField]
        private List<BaseGraph> _graphs = new List<BaseGraph>();

        /// <summary>Human-readable label. Falls back to the asset name when empty.</summary>
        public string Label => string.IsNullOrEmpty(_label) ? name : _label;

        /// <summary>Graphs in this group. May contain stale null entries if a member graph asset was deleted.</summary>
        public IReadOnlyList<BaseGraph> Graphs => _graphs;

        /// <summary>True when <paramref name="graph"/> is a member of this group.</summary>
        public bool Contains(BaseGraph graph) => graph != null && _graphs.Contains(graph);

        /// <summary>Adds <paramref name="graph"/> to this group. No-op if null or already a member.</summary>
        public void Add(BaseGraph graph)
        {
            if (graph != null && !_graphs.Contains(graph))
                _graphs.Add(graph);
        }

        /// <summary>Removes <paramref name="graph"/> from this group. No-op if not a member.</summary>
        public void Remove(BaseGraph graph) => _graphs.Remove(graph);
    }
}
