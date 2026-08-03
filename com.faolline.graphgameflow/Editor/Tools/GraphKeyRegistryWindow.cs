using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Lists every <see cref="BaseGraph"/> asset in the project with its <see cref="BaseGraph.GraphId"/>, and,
    /// per registered <see cref="IGraphKeySourceProvider"/>, whether it currently resolves as one of that
    /// source's keys — with a "Mark as {SourceLabel}" button to promote it. The graph-side counterpart to the
    /// scene name field's "Mark as …" helper, but a standalone window (mirroring <c>GraphValidator</c>'s own
    /// <c>Faolline ▸ Graph ▸ …</c> menu convention) rather than a per-field drawer: unlike a scene name, a
    /// graph's <see cref="BaseGraph.GraphId"/> is not author-typed anywhere — it's a stable GUID auto-assigned
    /// on the asset itself, so there is no inspector field for a dropdown drawer to attach to.
    /// </summary>
    public class GraphKeyRegistryWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Faolline/Graph/Graph Key Registry")]
        private static void Open() => GetWindow<GraphKeyRegistryWindow>("Graph Key Registry");

        private void OnGUI()
        {
            var providers = GraphKeySourceRegistry.Providers;
            if (providers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No graph key source registered — install an adapter (e.g. com.faolline.graphgameflow.addressables) to promote graphs to keys.",
                    MessageType.Info);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:BaseGraph");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var assetGuid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                var graph = AssetDatabase.LoadAssetAtPath<BaseGraph>(path);
                if (graph == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(graph.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("GraphId", graph.GraphId);

                foreach (var provider in providers)
                {
                    var resolved = provider.TryResolveGuid(assetGuid, out var key);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(provider.SourceLabel, resolved ? $"key: {key}" : "not promoted");
                    using (new EditorGUI.DisabledScope(resolved || !provider.CanPromote(path, graph.GraphId)))
                    {
                        if (GUILayout.Button($"Mark as {provider.SourceLabel}", GUILayout.Width(160)))
                            provider.Promote(path, graph.GraphId);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
