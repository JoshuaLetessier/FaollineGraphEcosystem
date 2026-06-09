using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Generates the reference scene-flow as a runnable <see cref="GameFlowGraph"/> asset:
    /// <c>start → [enter: Load Scene A] → await "advance" → [enter: Load Scene B] → end</c>, with the two
    /// <see cref="LoadSceneAction"/> sub-assets attached. Assign the result to a <c>GraphFlowDriver</c> and
    /// press Play. Menu: <c>Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample</c>.
    /// </summary>
    public static class GameFlowSampleBuilder
    {
        private const string PackageFolder = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow";
        private const string Folder        = PackageFolder + "/Samples";

        [MenuItem("Faolline/GraphGameFlow/Create Reference Scene-Flow Sample")]
        public static void CreateSampleMenu() => CreateSample();

        /// <summary>Builds and saves the reference scene-flow asset; returns the created graph.</summary>
        public static GameFlowGraph CreateSample()
        {
            EnsureFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/GameFlowReferenceSample.asset");

            var g = ScriptableObject.CreateInstance<GameFlowGraph>();
            AssetDatabase.CreateAsset(g, path);

            // Scene-load actions are sub-assets so the sample is self-contained and portable.
            var loadA = Sub<LoadSceneAction>(g, "LoadSceneA"); loadA.SceneName = "A"; loadA.Mode = LoadSceneMode.Single;
            var loadB = Sub<LoadSceneAction>(g, "LoadSceneB"); loadB.SceneName = "B"; loadB.Mode = LoadSceneMode.Single;

            var start = new StartNodeData     { Id = NewId(), NodeType = StartNodeData.NodeTypeId,     Title = "Start",        Position = new Vector2(0,   0) };
            var nodeA = new StatementNodeData { Id = NewId(), NodeType = StatementNodeData.NodeTypeId, Title = "Load Scene A", Position = new Vector2(240, 0) };
            nodeA.OnEnterActions.Add(loadA);
            var gate  = new StatementNodeData { Id = NewId(), NodeType = StatementNodeData.NodeTypeId, Title = "Await advance", AwaitSignalName = "advance", Position = new Vector2(480, 0) };
            var nodeB = new StatementNodeData { Id = NewId(), NodeType = StatementNodeData.NodeTypeId, Title = "Load Scene B", Position = new Vector2(720, 0) };
            nodeB.OnEnterActions.Add(loadB);
            var end   = new EndNodeData       { Id = NewId(), NodeType = EndNodeData.NodeTypeId,       EndReason = EndReason.Completed, Title = "End", Position = new Vector2(960, 0) };

            g.AddNode(start); g.AddNode(nodeA); g.AddNode(gate); g.AddNode(nodeB); g.AddNode(end);
            g.EntryNodeId = start.Id;
            g.AddEdge(Edge(start.Id, nodeA.Id, "out"));
            g.AddEdge(Edge(nodeA.Id, gate.Id,  "out"));
            g.AddEdge(Edge(gate.Id,  nodeB.Id, "out"));
            g.AddEdge(Edge(nodeB.Id, end.Id,   "out"));

            EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = g;
            EditorGUIUtility.PingObject(g);
            Debug.Log($"[GraphGameFlow] Sample created: {path}. Assign it to a GraphFlowDriver and press Play — " +
                      "scene A loads, the flow waits, then RaiseSignal(\"advance\") loads scene B.");
            return g;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string NewId() => System.Guid.NewGuid().ToString("D");

        private static BaseEdgeData Edge(string from, string to, string port)
            => new BaseEdgeData { Id = NewId(), FromNodeId = from, ToNodeId = to, PortName = port };

        private static T Sub<T>(Object owner, string name) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = name;
            AssetDatabase.AddObjectToAsset(obj, owner);
            return obj;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            AssetDatabase.CreateFolder(PackageFolder, "Samples");
        }
    }
}
