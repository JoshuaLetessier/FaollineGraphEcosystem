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

            // The Test* actions/conditions use the raw-string context channel (TestContextKeys), so no parameter
            // declaration is needed: intro's enter-action sets DoorOpen and the conditions read it back on the
            // same raw keys (absent bool reads as false).

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

        // ── Feature 008 sample (EndReason + SubGraph + typed params) ───────────────

        private const string ChildPath  = Folder + "/SampleChildGraph.asset";
        private const string AuthoringPath = Folder + "/SampleAuthoringGraph.asset";

        /// <summary>
        /// Generates a parent + child graph pair that exercises feature 008 end to end:
        /// a SubGraph node descending into the child, typed Int/Float/String parameters with
        /// typed actions/conditions gating choices, and three End nodes with distinct end reasons.
        /// Menu: <c>Faolline/Create Sample TestGraph 008 (Authoring)</c>.
        /// </summary>
        [MenuItem("Faolline/Create Sample TestGraph 008 (Authoring)")]
        public static void CreateAuthoringSample()
        {
            EnsureFolder();

            const string score = "score";
            const string hp    = "hp";
            const string pname = "name";

            // ── Child graph: Start → Statement → End ──────────────────────────────
            var child = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(child, ChildPath);

            var childLog = Sub<TestLogAction>(child, "ChildLog"); childLog.Message = "Inside sub-graph";
            var cStart = new StartNodeData         { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var cStmt  = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Child", Position = new Vector2(240, 0) };
            cStmt.OnEnterActions.Add(childLog);
            var cEnd   = new EndNodeData           { Id = NewId(), NodeType = EndNodeData.NodeTypeId, Position = new Vector2(480, 0) };
            child.AddNode(cStart); child.AddNode(cStmt); child.AddNode(cEnd);
            child.EntryNodeId = cStart.Id;
            child.AddEdge(Edge(cStart.Id, cStmt.Id, "out"));
            child.AddEdge(Edge(cStmt.Id,  cEnd.Id,  "out"));
            EditorUtility.SetDirty(child);

            // ── Parent graph ──────────────────────────────────────────────────────
            var parent = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(parent, AuthoringPath);

            // The Test* typed actions/conditions use the raw-string context channel: the Setup node's enter
            // actions set score/hp/name before the choice reads them, so no parameter declaration is needed.

            // Typed actions (US3)
            var logSetup = Sub<TestLogAction>(parent, "LogSetup"); logSetup.Message = "Setup: score=5, hp=0.3, name=hero";
            var setScore = Sub<TestSetIntAction>(parent, "SetScore");    setScore.ParameterKey = score; setScore.Value = 5;
            var setHp    = Sub<TestSetFloatAction>(parent, "SetHp");     setHp.ParameterKey    = hp;    setHp.Value    = 0.3f;
            var setName  = Sub<TestSetStringAction>(parent, "SetName");  setName.ParameterKey  = pname; setName.Value  = "hero";

            // Typed conditions (US3)
            var winCond = Sub<TestIntCondition>(parent, "ScoreHighCond"); winCond.ParameterKey = score; winCond.Operator = ComparisonOperator.GreaterOrEqual; winCond.ExpectedValue = 3;   // 5 >= 3 → pass
            var retreatCond = Sub<TestFloatCondition>(parent, "HpLowCond"); retreatCond.ParameterKey = hp; retreatCond.Operator = ComparisonOperator.Less;     retreatCond.ExpectedValue = 0.5f; // 0.3 < 0.5 → pass
            var villainCond = Sub<TestStringCondition>(parent, "IsVillainCond"); villainCond.ParameterKey = pname; villainCond.ExpectedValue = "villain";          // "hero" != "villain" → fail

            var logWin       = Sub<TestLogAction>(parent, "LogWin");       logWin.Message       = "Branch: WIN";
            var logRetreat   = Sub<TestLogAction>(parent, "LogRetreat");   logRetreat.Message   = "Branch: RETREAT";
            var logSurrender = Sub<TestLogAction>(parent, "LogSurrender"); logSurrender.Message = "Branch: SURRENDER";

            // Nodes
            var start = new StartNodeData         { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var setup = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Setup", Position = new Vector2(240, 0) };
            setup.OnEnterActions.Add(logSetup);
            setup.OnEnterActions.Add(setScore);
            setup.OnEnterActions.Add(setHp);
            setup.OnEnterActions.Add(setName);

            // US2 — SubGraph node, inheriting the parent context so the typed params survive the descent
            var sub = new SubGraphNodeData { Id = NewId(), NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true, Position = new Vector2(480, 0) };

            var choice = new ChoiceNodeData { Id = NewId(), NodeType = ChoiceNodeData.NodeTypeId, Position = new Vector2(720, 0) };
            var winChoice       = new TestChoice { Id = NewId(), Label = "Win (score >= 3)",        Condition = winCond };
            var retreatChoice   = new TestChoice { Id = NewId(), Label = "Retreat (hp < 0.5)",      Condition = retreatCond };
            var surrenderChoice = new TestChoice { Id = NewId(), Label = "Surrender (name=villain)", Condition = villainCond };
            choice.Choices.Add(winChoice);
            choice.Choices.Add(retreatChoice);
            choice.Choices.Add(surrenderChoice);

            var winPath       = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Win",       Position = new Vector2(980, -180) }; winPath.OnEnterActions.Add(logWin);
            var retreatPath   = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Retreat",   Position = new Vector2(980, 0) };    retreatPath.OnEnterActions.Add(logRetreat);
            var surrenderPath = new TestStatementNodeData { Id = NewId(), NodeType = TestStatementNodeData.NodeTypeId, Label = "Surrender", Position = new Vector2(980, 180) };  surrenderPath.OnEnterActions.Add(logSurrender);

            // US1 — three End nodes with distinct end reasons
            var endWin       = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, Position = new Vector2(1240, -180) };
            var endRetreat   = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled, Position = new Vector2(1240, 0) };
            var endSurrender = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Error,     Position = new Vector2(1240, 180) };

            parent.AddNode(start); parent.AddNode(setup); parent.AddNode(sub); parent.AddNode(choice);
            parent.AddNode(winPath); parent.AddNode(retreatPath); parent.AddNode(surrenderPath);
            parent.AddNode(endWin); parent.AddNode(endRetreat); parent.AddNode(endSurrender);
            parent.EntryNodeId = start.Id;

            parent.AddEdge(Edge(start.Id,         setup.Id,         "out"));
            parent.AddEdge(Edge(setup.Id,         sub.Id,           "out"));
            parent.AddEdge(Edge(sub.Id,           choice.Id,        "out"));
            parent.AddEdge(Edge(choice.Id,        winPath.Id,       winChoice.Id));
            parent.AddEdge(Edge(choice.Id,        retreatPath.Id,   retreatChoice.Id));
            parent.AddEdge(Edge(choice.Id,        surrenderPath.Id, surrenderChoice.Id));
            parent.AddEdge(Edge(winPath.Id,       endWin.Id,        "out"));
            parent.AddEdge(Edge(retreatPath.Id,   endRetreat.Id,    "out"));
            parent.AddEdge(Edge(surrenderPath.Id, endSurrender.Id,  "out"));

            EditorUtility.SetDirty(parent);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = parent;
            EditorGUIUtility.PingObject(parent);
            Debug.Log($"[GraphTest] 008 authoring sample created: {AuthoringPath} (+ child {ChildPath}). " +
                      "Open the parent, press Run → it descends into the sub-graph, then pauses at the Choice; " +
                      "Win/Retreat are offered (Surrender is filtered out). Win → Completed, Retreat → Cancelled.");
        }

        // ── History-depth stress sample ────────────────────────────────────────────

        /// <summary>
        /// Generates a long linear chain (Start → N statements → End) with a small HistoryDepth so
        /// the GoBack history cap is observable: after Run, GoBack rewinds only ~HistoryDepth steps
        /// before it can go no further. Menu: <c>Faolline/Create Sample TestGraph — History Depth Stress</c>.
        /// </summary>
        [MenuItem("Faolline/Create Sample TestGraph — History Depth Stress")]
        public static void CreateHistoryStressSample()
        {
            EnsureFolder();

            const int steps  = 50;
            const int depth  = 10;
            const int perRow = 10;

            var g = ScriptableObject.CreateInstance<TestGraph>();
            AssetDatabase.CreateAsset(g, Folder + "/SampleHistoryStress.asset");
            g.HistoryDepth = depth;

            var start = new StartNodeData { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = GridPos(0, perRow) };
            g.AddNode(start);
            g.EntryNodeId = start.Id;

            string prev = start.Id;
            for (int i = 1; i <= steps; i++)
            {
                var node = new TestStatementNodeData
                {
                    Id = NewId(),
                    NodeType = TestStatementNodeData.NodeTypeId,
                    Label = $"Step {i}",
                    Position = GridPos(i, perRow)
                };
                g.AddNode(node);
                g.AddEdge(Edge(prev, node.Id, "out"));
                prev = node.Id;
            }

            var end = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, Position = GridPos(steps + 1, perRow) };
            g.AddNode(end);
            g.AddEdge(Edge(prev, end.Id, "out"));

            EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = g;
            EditorGUIUtility.PingObject(g);
            Debug.Log($"[GraphTest] History-depth stress sample: {steps} steps, HistoryDepth={depth}. " +
                      $"Run, then click ← GoBack repeatedly — it rewinds only ~{depth} steps before stopping " +
                      "(older history is dropped, so you cannot reach Start). Raise HistoryDepth on the asset to rewind further.");
        }

        private static Vector2 GridPos(int index, int perRow)
        {
            int col = index % perRow;
            int row = index / perRow;
            return new Vector2(col * 200f, row * 130f);
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
