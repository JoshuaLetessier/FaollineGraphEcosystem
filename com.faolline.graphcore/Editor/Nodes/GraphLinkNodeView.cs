using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Canvas view for a <see cref="GraphLinkNodeData"/> — a distinct, openable documentary reference showing the
    /// target graph's kind + name ("📎 Quest: Relics") or "📎 (missing target)". It never represents execution.
    /// Double-clicking opens the target in its editor via <see cref="GraphEditorWindowRegistry"/>.
    /// </summary>
    public sealed class GraphLinkNodeView : BaseNodeView
    {
        private const string UssName = "GraphLinkNodeView";

        public GraphLinkNodeView(GraphLinkNodeData data)
        {
            title = Describe(data);
            Initialize(data);
        }

        private GraphLinkNodeData Link => NodeData as GraphLinkNodeData;

        /// <inheritdoc/>
        protected override void OnBuildView()
        {
            AddToClassList("graphlink-node");
            LoadOwnStyleSheet();

            if (!string.IsNullOrEmpty(Link?.Note))
            {
                var note = new Label(Link.Note);
                note.AddToClassList("graphlink-note");
                extensionContainer.Add(note);
                RefreshExpandedState();
            }

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount != 2) return;
                GraphEditorWindowRegistry.Open(Link?.TargetGraph);
                evt.StopPropagation();
            });
        }

        // "📎 Quest: Relics" / "📎 (missing target)" — Kind derived generically from the graph TYPE name.
        private static string Describe(GraphLinkNodeData data)
        {
            var g = data?.TargetGraph;
            return g == null ? "📎 (missing target)" : $"📎 {KindOf(g)}: {NameOf(g)}";
        }

        private static string KindOf(BaseGraph g)
        {
            var n = g.GetType().Name;                                  // "QuestGraph" -> "Quest"
            return n.EndsWith("Graph") && n.Length > 5 ? n.Substring(0, n.Length - 5) : n;
        }

        private static string NameOf(BaseGraph g)
            => !string.IsNullOrEmpty(g.name) ? g.name
             : string.IsNullOrEmpty(g.GraphId) ? "(unnamed)" : g.GraphId;

        private void LoadOwnStyleSheet()
        {
            foreach (var guid in AssetDatabase.FindAssets($"{UssName} t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"{UssName}.uss")) continue;
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null) { styleSheets.Add(sheet); break; }
            }
        }
    }
}
