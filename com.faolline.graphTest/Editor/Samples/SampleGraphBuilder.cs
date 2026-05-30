using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Editor utility that generates a self-contained sample <see cref="TestGraph"/> asset
    /// exercising every feature end-to-end: bool parameters, enter/exit actions, an entry
    /// condition, a checkpoint node, and a Choice node with a conditional choice.
    /// Conditions and actions are stored as sub-assets so the graph is fully portable.
    /// <para>Menu: <c>Faolline/Create Sample TestGraph (Full)</c>. After generating, open the
    /// asset (double-click) and press <b>Run</b>; at the Choice node press <b>Choose</b>.</para>
    /// </summary>
    public static class SampleGraphBuilder
    {
        private const string Folder = "Assets/FaollineGraphEcosystem/com.faolline.graphTest/Samples";
        private const string AssetPath = Folder + "/SampleCompleteGraph.asset";

        [MenuItem("Faolline/Create Sample TestGraph (Full)")]
        public static void CreateSampleGraph()
        {
            EnsureFolder();

            var graph = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(graph, AssetPath);

            // ── Parameters (seed the context so conditions can read them) ─────────
            graph.AddParameter(new ParameterData { Key = TestContextKeys.DoorOpen, Type = ParameterType.Bool, DefaultValue = "false" });
            graph.AddParameter(new ParameterData { Key = TestContextKeys.FlagA,    Type = ParameterType.Bool, DefaultValue = "false" });

            // ── Sub-assets: actions & conditions ─────────────────────────────────
            var logIntro     = Sub<TestLogAction>(graph, "LogIntro");        logIntro.Message = "Entrée intro";
            var setDoorOpen  = Sub<TestSetBoolAction>(graph, "SetDoorOpen"); setDoorOpen.ParameterKey = TestContextKeys.DoorOpen; setDoorOpen.Value = true;
            var logExitIntro = Sub<TestLogAction>(graph, "LogExitIntro");    logExitIntro.Message = "Sortie intro";
            var logCheckpoint= Sub<TestLogAction>(graph, "LogCheckpoint");   logCheckpoint.Message = "Checkpoint atteint";
            var doorCond     = Sub<TestBoolCondition>(graph, "DoorOpenCond");doorCond.ParameterKey = TestContextKeys.DoorOpen; doorCond.ExpectedValue = true;
            var logDoorPath  = Sub<TestLogAction>(graph, "LogDoorPath");     logDoorPath.Message = "Branche: porte ouverte";
            var logLeavePath = Sub<TestLogAction>(graph, "LogLeavePath");    logLeavePath.Message = "Branche: tu pars";

            // ── Nodes ─────────────────────────────────────────────────────────────
            var start = new StartNodeData { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };

            var intro = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Intro", Position = new Vector2(240, 0) };
            intro.OnEnterActions.Add(logIntro);
            intro.OnEnterActions.Add(setDoorOpen);   // opens the door so the conditional choice becomes available
            intro.OnExitActions.Add(logExitIntro);

            var checkpoint = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Checkpoint", Position = new Vector2(480, 0), IsCheckpoint = true };
            checkpoint.OnEnterActions.Add(logCheckpoint);

            var choice = new ChoiceNodeData { Id = NewId(), NodeType = ChoiceNodeData.NodeTypeId, Position = new Vector2(720, 0) };
            var openDoorChoice = new TestChoice { Id = NewId(), Label = "Ouvrir la porte", Condition = doorCond };
            var leaveChoice    = new TestChoice { Id = NewId(), Label = "Partir" };
            choice.Choices.Add(openDoorChoice);
            choice.Choices.Add(leaveChoice);

            var doorPath = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Porte", Position = new Vector2(980, -140) };
            doorPath.EntryConditions.Add(doorCond);  // entry condition exercised here
            doorPath.OnEnterActions.Add(logDoorPath);

            var leavePath = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Sortie", Position = new Vector2(980, 140) };
            leavePath.OnEnterActions.Add(logLeavePath);

            var end = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, Position = new Vector2(1240, 0) };

            graph.AddNode(start);
            graph.AddNode(intro);
            graph.AddNode(checkpoint);
            graph.AddNode(choice);
            graph.AddNode(doorPath);
            graph.AddNode(leavePath);
            graph.AddNode(end);
            graph.EntryNodeId = start.Id;

            // ── Edges (choice edges route by the choice GUID = its port name) ─────
            graph.AddEdge(Edge(start.Id,      intro.Id,      "out"));
            graph.AddEdge(Edge(intro.Id,      checkpoint.Id, "out"));
            graph.AddEdge(Edge(checkpoint.Id, choice.Id,     "out"));
            graph.AddEdge(Edge(choice.Id,     doorPath.Id,   openDoorChoice.Id));
            graph.AddEdge(Edge(choice.Id,     leavePath.Id,  leaveChoice.Id));
            graph.AddEdge(Edge(doorPath.Id,   end.Id,        "out"));
            graph.AddEdge(Edge(leavePath.Id,  end.Id,        "out"));

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            Debug.Log($"[GraphTest] Sample graph created at {AssetPath}. Double-click to open, press Run, then Choose at the Choice node.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string NewId() => System.Guid.NewGuid().ToString("D");

        private static BaseEdgeData Edge(string from, string to, string portName)
            => new BaseEdgeData { Id = NewId(), FromNodeId = from, ToNodeId = to, PortName = portName };

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
            const string parent = "Assets/FaollineGraphEcosystem/com.faolline.graphTest";
            AssetDatabase.CreateFolder(parent, "Samples");
        }
    }
}
