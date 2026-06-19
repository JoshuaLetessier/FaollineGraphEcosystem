using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Editor window for <see cref="QuestGraph"/> assets. Opens via <c>Faolline/Open Quest Graph Editor</c> or by
    /// double-clicking a quest asset. Authoring only — a quest is evaluated reactively (no runner), so there is no
    /// Run loop; the base toolbar's Save / Arrange / ↻ Refresh apply. One window per asset (focus-or-create).
    /// </summary>
    public sealed class QuestGraphEditorWindow : BaseGraphEditorWindow
    {
        private QuestNodeInspectorView _inspector;

        /// <summary>Test hook: loads a graph into the window without the asset-open flow.</summary>
        public void LoadGraphForTest(QuestGraph graph) => LoadGraph(graph);

        [MenuItem("Faolline/Open Quest Graph Editor")]
        public static void Open() => GetWindow<QuestGraphEditorWindow>("Quest Graph Editor");

        /// <summary>Opens <paramref name="graph"/> in its OWN window (used by GraphLink navigation) — reuses a
        /// window already showing this graph, otherwise creates one; never steals another open editor.</summary>
        public static void Open(QuestGraph graph)
        {
            if (graph == null) return;
            foreach (var existing in Resources.FindObjectsOfTypeAll<QuestGraphEditorWindow>())
                if (existing.LoadedGraph == graph) { existing.Focus(); return; }
            var window = CreateWindow<QuestGraphEditorWindow>();
            window.titleContent = new GUIContent(graph.name);
            window.LoadGraph(graph);
        }

        [InitializeOnLoadMethod]
        private static void RegisterForGraphLinkNavigation() =>
            GraphEditorWindowRegistry.Register(typeof(QuestGraph), g => Open((QuestGraph)g));

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as QuestGraph;
            if (asset == null) return false;

            foreach (var existing in Resources.FindObjectsOfTypeAll<QuestGraphEditorWindow>())
                if (existing.LoadedGraph == asset) { existing.Focus(); return true; }

            var window = CreateWindow<QuestGraphEditorWindow>();
            window.titleContent = new GUIContent(asset.name);
            window.LoadGraph(asset);
            return true;
        }

        protected override BaseGraphView CreateGraphView() => new QuestGraphView();

        protected override BaseNodeInspectorView CreateNodeInspectorView()
        {
            _inspector = new QuestNodeInspectorView();
            return _inspector;
        }

        protected override void OnGraphLoaded(BaseGraph graph) => _inspector?.SetGraph(graph);
    }
}
