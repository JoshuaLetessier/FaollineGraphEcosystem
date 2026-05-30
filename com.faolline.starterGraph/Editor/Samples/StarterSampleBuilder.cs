using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Generates a self-contained sample <see cref="StarterGraph"/> (parent + child) that exercises
    /// the whole starter: typed Int/Float/String parameters with typed actions/conditions, a SubGraph
    /// descent, a checkpoint, a Choice gated by typed conditions, and three End nodes with distinct
    /// end reasons. Conditions/actions are sub-assets so the graphs are portable.
    /// Menu: <c>Faolline/Create Starter Sample Graph</c>.
    /// </summary>
    public static class StarterSampleBuilder
    {
        private const string Folder        = "Assets/FaollineGraphEcosystem/com.faolline.starterGraph/Samples";
        private const string ChildPath     = Folder + "/StarterSampleChild.asset";
        private const string SamplePath    = Folder + "/StarterSampleGraph.asset";

        [MenuItem("Faolline/Create Starter Sample Graph")]
        public static void CreateSample()
        {
            EnsureFolder();

            // ── Child graph: Start → Statement(log) → End ─────────────────────────
            var child = ScriptableObject.CreateInstance<StarterGraph>();
            AssetDatabase.CreateAsset(child, ChildPath);
            var childLog = Sub<StarterLogAction>(child, "ChildLog"); childLog.Message = "Inside sub-graph";
            var cStart = new StartNodeData            { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var cStmt  = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Child", Position = new Vector2(240, 0) };
            cStmt.OnEnterActions.Add(childLog);
            var cEnd   = new EndNodeData              { Id = NewId(), NodeType = EndNodeData.NodeTypeId, Position = new Vector2(480, 0) };
            child.AddNode(cStart); child.AddNode(cStmt); child.AddNode(cEnd);
            child.EntryNodeId = cStart.Id;
            child.AddEdge(Edge(cStart.Id, cStmt.Id, "out"));
            child.AddEdge(Edge(cStmt.Id,  cEnd.Id,  "out"));
            EditorUtility.SetDirty(child);

            // ── Parent graph ──────────────────────────────────────────────────────
            var g = ScriptableObject.CreateInstance<StarterGraph>();
            AssetDatabase.CreateAsset(g, SamplePath);

            // Typed parameters (keys via StarterContextKeys — Principle VI)
            g.AddParameter(new ParameterData { Key = StarterContextKeys.Score, Type = ParameterType.Int,    DefaultValue = "0" });
            g.AddParameter(new ParameterData { Key = StarterContextKeys.Ratio, Type = ParameterType.Float,  DefaultValue = "1" });
            g.AddParameter(new ParameterData { Key = StarterContextKeys.Label, Type = ParameterType.String, DefaultValue = "" });

            // Typed actions
            var logSetup = Sub<StarterLogAction>(g, "LogSetup"); logSetup.Message = "Setup: score=5, ratio=0.3, label=hero";
            var setScore = Sub<StarterSetIntAction>(g, "SetScore");    setScore.ParameterKey = StarterContextKeys.Score; setScore.Value = 5;
            var setRatio = Sub<StarterSetFloatAction>(g, "SetRatio");  setRatio.ParameterKey = StarterContextKeys.Ratio; setRatio.Value = 0.3f;
            var setLabel = Sub<StarterSetStringAction>(g, "SetLabel"); setLabel.ParameterKey = StarterContextKeys.Label; setLabel.Value = "hero";

            // Typed conditions
            var winCond     = Sub<StarterIntCondition>(g, "ScoreHighCond");  winCond.ParameterKey     = StarterContextKeys.Score; winCond.Operator     = ComparisonOperator.GreaterOrEqual; winCond.ExpectedValue     = 3;     // 5 >= 3 → pass
            var retreatCond = Sub<StarterFloatCondition>(g, "RatioLowCond"); retreatCond.ParameterKey = StarterContextKeys.Ratio; retreatCond.Operator = ComparisonOperator.Less;           retreatCond.ExpectedValue = 0.5f;  // 0.3 < 0.5 → pass
            var hiddenCond  = Sub<StarterStringCondition>(g, "IsVillainCond"); hiddenCond.ParameterKey = StarterContextKeys.Label; hiddenCond.ExpectedValue = "villain";                                                      // "hero" != "villain" → fail

            var logWin     = Sub<StarterLogAction>(g, "LogWin");     logWin.Message     = "Branch: WIN";
            var logRetreat = Sub<StarterLogAction>(g, "LogRetreat"); logRetreat.Message = "Branch: RETREAT";
            var logHidden  = Sub<StarterLogAction>(g, "LogHidden");  logHidden.Message  = "Branch: HIDDEN";

            // Nodes
            var start = new StartNodeData            { Id = NewId(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var setup = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Setup", Position = new Vector2(240, 0), IsCheckpoint = true };
            setup.OnEnterActions.Add(logSetup);
            setup.OnEnterActions.Add(setScore);
            setup.OnEnterActions.Add(setRatio);
            setup.OnEnterActions.Add(setLabel);

            var sub = new SubGraphNodeData { Id = NewId(), NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true, Position = new Vector2(480, 0) };

            var choice = new ChoiceNodeData { Id = NewId(), NodeType = ChoiceNodeData.NodeTypeId, Position = new Vector2(720, 0) };
            var winChoice     = new StarterChoice { Id = NewId(), Label = "Win (score >= 3)",         Condition = winCond };
            var retreatChoice = new StarterChoice { Id = NewId(), Label = "Retreat (ratio < 0.5)",    Condition = retreatCond };
            var hiddenChoice  = new StarterChoice { Id = NewId(), Label = "Hidden (label=villain)",   Condition = hiddenCond };
            choice.Choices.Add(winChoice);
            choice.Choices.Add(retreatChoice);
            choice.Choices.Add(hiddenChoice);

            var winPath     = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Win",     Position = new Vector2(980, -180) }; winPath.OnEnterActions.Add(logWin);
            var retreatPath = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Retreat", Position = new Vector2(980, 0) };    retreatPath.OnEnterActions.Add(logRetreat);
            var hiddenPath  = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Hidden",  Position = new Vector2(980, 180) };  hiddenPath.OnEnterActions.Add(logHidden);

            var endWin    = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, Position = new Vector2(1240, -180) };
            var endRetreat= new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled, Position = new Vector2(1240, 0) };
            var endHidden = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Error,     Position = new Vector2(1240, 180) };

            g.AddNode(start); g.AddNode(setup); g.AddNode(sub); g.AddNode(choice);
            g.AddNode(winPath); g.AddNode(retreatPath); g.AddNode(hiddenPath);
            g.AddNode(endWin); g.AddNode(endRetreat); g.AddNode(endHidden);
            g.EntryNodeId = start.Id;

            g.AddEdge(Edge(start.Id,       setup.Id,       "out"));
            g.AddEdge(Edge(setup.Id,       sub.Id,         "out"));
            g.AddEdge(Edge(sub.Id,         choice.Id,      "out"));
            g.AddEdge(Edge(choice.Id,      winPath.Id,     winChoice.Id));
            g.AddEdge(Edge(choice.Id,      retreatPath.Id, retreatChoice.Id));
            g.AddEdge(Edge(choice.Id,      hiddenPath.Id,  hiddenChoice.Id));
            g.AddEdge(Edge(winPath.Id,     endWin.Id,      "out"));
            g.AddEdge(Edge(retreatPath.Id, endRetreat.Id,  "out"));
            g.AddEdge(Edge(hiddenPath.Id,  endHidden.Id,   "out"));

            EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = g;
            EditorGUIUtility.PingObject(g);
            Debug.Log($"[StarterGraph] Sample created: {SamplePath} (+ child {ChildPath}). " +
                      "Open it, press Run → descends the sub-graph, pauses at the Choice (Win + Retreat offered, " +
                      "Hidden filtered out). Win → Completed, Retreat → Cancelled.");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

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
            AssetDatabase.CreateFolder("Assets/FaollineGraphEcosystem/com.faolline.starterGraph", "Samples");
        }
    }
}
