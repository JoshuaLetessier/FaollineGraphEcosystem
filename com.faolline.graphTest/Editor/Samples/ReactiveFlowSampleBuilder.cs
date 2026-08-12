using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphTest;
using Faolline.GraphLogging;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Generates two sample <see cref="TestGraph"/> assets for the non-linear engines, so the in-game run-cursor
    /// (v2) can be seen on them: a <b>reactive progression DAG</b> (prerequisite gates → Locked/Available/
    /// Completed) and a <b>flow fork/join</b> (one fire fans out, then joins). Open either in the TestGraph
    /// window, drop the matching sample driver (<c>ReactiveSampleDriver</c> / <c>FlowSampleDriver</c>) on a
    /// GameObject with the graph assigned, and press Play — the editor map lights up live.
    /// <para>Menu: <c>Faolline/Create Sample Reactive + Flow Graphs</c>.</para>
    /// </summary>
    public static class ReactiveFlowSampleBuilder
    {
        private const string Folder       = "Assets/FaollineGraphEcosystem/com.faolline.graphTest/Samples";
        private const string ReactivePath = Folder + "/SampleReactiveProgression.asset";
        private const string FlowPath     = Folder + "/SampleFlowFork.asset";

        [MenuItem("Faolline/Create Sample Reactive + Flow Graphs")]
        public static void CreateBoth()
        {
            EnsureFolder();
            CreateReactive();
            CreateFlow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Logging.Info("GraphTest", $"[GraphTest] Reactive + Flow samples created:\n  {ReactivePath}\n  {FlowPath}\n" +
                      "Open one, add its sample driver (ReactiveSampleDriver / FlowSampleDriver) to a scene " +
                      "GameObject with the graph assigned, then press Play to watch the run-cursor map.");
        }

        // ── Reactive progression DAG ────────────────────────────────────────────────
        // A,B,C have no prerequisites (Available at start). Gate needs A AND B. Region needs Gate AND C.
        private static void CreateReactive()
        {
            var g = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(g, ReactivePath);

            var a    = Stmt(g, "Key A",       0,   -150);
            var b    = Stmt(g, "Key B",       0,      0);
            var c    = Stmt(g, "Switch C",    0,    150);
            var gate = Stmt(g, "Gate (A+B)",  300,  -75);
            var done = Stmt(g, "Region Done", 600,    0);

            g.EntryNodeId = a.Id;   // reactive ignores it, but keep the graph well-formed for the window

            // Edge From→To means From is a PREREQUISITE of To.
            g.AddEdge(Edge(a.Id,    gate.Id));
            g.AddEdge(Edge(b.Id,    gate.Id));
            g.AddEdge(Edge(gate.Id, done.Id));
            g.AddEdge(Edge(c.Id,    done.Id));

            EditorUtility.SetDirty(g);
        }

        // ── Flow fork / join ────────────────────────────────────────────────────────
        // Firing Trigger fans out to Left and Right; Merge joins both (AND), then Done fires.
        private static void CreateFlow()
        {
            var g = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(g, FlowPath);

            var trigger = Stmt(g, "Trigger", 0,      0);
            var left    = Stmt(g, "Left",    300, -120);
            var right   = Stmt(g, "Right",   300,  120);
            var merge   = Stmt(g, "Merge",   600,    0);
            var done    = Stmt(g, "Done",    900,    0);

            g.EntryNodeId = trigger.Id;

            g.AddEdge(Edge(trigger.Id, left.Id));
            g.AddEdge(Edge(trigger.Id, right.Id));
            g.AddEdge(Edge(left.Id,    merge.Id));
            g.AddEdge(Edge(right.Id,   merge.Id));
            g.AddEdge(Edge(merge.Id,   done.Id));

            EditorUtility.SetDirty(g);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static TestStatementNodeData Stmt(BaseGraph owner, string label, float x, float y)
        {
            var node = new TestStatementNodeData
            {
                Id = NewId(),
                NodeType = TestStatementNodeData.NodeTypeId,
                Label = label,
                Position = new Vector2(x, y)
            };
            owner.AddNode(node);
            return node;
        }

        private static string NewId() => System.Guid.NewGuid().ToString("D");

        private static BaseEdgeData Edge(string from, string to)
            => new BaseEdgeData { Id = NewId(), FromNodeId = from, ToNodeId = to, PortName = "out" };

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            AssetDatabase.CreateFolder("Assets/FaollineGraphEcosystem/com.faolline.graphTest", "Samples");
        }
    }
}
