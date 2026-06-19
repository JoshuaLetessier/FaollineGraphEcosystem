using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Editor window for authoring <see cref="GameFlowGraph"/> assets. Opens via
    /// <c>Faolline/Open GraphGameFlow Editor</c> or by double-clicking a <see cref="GameFlowGraph"/>. The
    /// toolbar offers Save (base) and Validate; running a flow is done with a <c>GraphFlowDriver</c> in Play.
    /// </summary>
    public class GameFlowGraphEditorWindow : BaseGraphEditorWindow
    {
        private GameFlowNodeInspectorView _inspector;

        [MenuItem("Faolline/Open GraphGameFlow Editor")]
        public static void Open() => GetWindow<GameFlowGraphEditorWindow>("GraphGameFlow Editor");

        /// <summary>Opens the editor with <paramref name="graph"/> loaded (used by GraphLink navigation).</summary>
        public static void Open(GameFlowGraph graph)
        {
            var window = GetWindow<GameFlowGraphEditorWindow>("GraphGameFlow Editor");
            window.LoadGraph(graph);
        }

        [InitializeOnLoadMethod]
        private static void RegisterForGraphLinkNavigation() =>
            GraphEditorWindowRegistry.Register(typeof(GameFlowGraph), g => Open((GameFlowGraph)g));

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as GameFlowGraph;
            if (asset == null) return false;

            foreach (var existing in Resources.FindObjectsOfTypeAll<GameFlowGraphEditorWindow>())
            {
                if (existing.LoadedGraph == asset)
                {
                    existing.Focus();
                    return true;
                }
            }

            var window = CreateWindow<GameFlowGraphEditorWindow>();
            window.titleContent = new GUIContent(asset.name);
            window.LoadGraph(asset);
            return true;
        }

        protected override BaseGraphView CreateGraphView() => new GameFlowGraphView();

        protected override BaseNodeInspectorView CreateNodeInspectorView()
        {
            _inspector = new GameFlowNodeInspectorView();
            return _inspector;
        }

        protected override void OnGraphLoaded(BaseGraph graph)
        {
            _inspector?.SetGraph(graph);
            _inspector?.SetGraphView(GraphView as GameFlowGraphView);
        }

        protected override void PopulateToolbar(Toolbar toolbar)
        {
            toolbar.Add(new ToolbarButton(ValidateGraph) { text = "Validate" });
        }

        private void ValidateGraph()
        {
            if (LoadedGraph == null)
            {
                Debug.LogWarning("[GraphGameFlow] No graph loaded to validate.");
                return;
            }

            var report = GraphValidator.Validate(LoadedGraph);
            if (report.Issues.Count == 0)
            {
                Debug.Log("[GraphGameFlow] Validation passed: no issues.");
                return;
            }

            foreach (var issue in report.Issues)
            {
                var msg = $"[GraphGameFlow] {issue.Severity}: {issue.Message}" +
                          (string.IsNullOrEmpty(issue.NodeId) ? "" : $" (node {issue.NodeId})");
                if (issue.Severity == GraphIssueSeverity.Error) Debug.LogError(msg);
                else if (issue.Severity == GraphIssueSeverity.Warning) Debug.LogWarning(msg);
                else Debug.Log(msg);
            }
        }
    }
}
