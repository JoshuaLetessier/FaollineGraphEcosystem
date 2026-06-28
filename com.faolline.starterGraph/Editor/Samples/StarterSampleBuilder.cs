using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Generates a small, self-contained sample <see cref="StarterGraph"/> that shows the template's extension
    /// points in one graph: the typed context (a declared <c>Flag</c> bool parameter seeded on run), a custom
    /// node (<see cref="StarterStatementNodeData"/>), a graphcore <see cref="LogAction"/> and a graphcore
    /// <see cref="BoolCondition"/> gating one branch of a Choice. Conditions/actions are sub-assets so the
    /// graph is portable. Menu: <c>Faolline/Create Starter Sample Graph</c>.
    /// <para>Shape: Start → Intro(log) → Choice(Left gated by Flag, Right always) → A/B(log) → End.</para>
    /// </summary>
    public static class StarterSampleBuilder
    {
        private const string Folder     = "Assets/FaollineGraphEcosystem/com.faolline.starterGraph/Samples";
        private const string SamplePath = Folder + "/StarterSampleGraph.asset";

        [MenuItem("Faolline/Create Starter Sample Graph")]
        public static void CreateSample()
        {
            EnsureFolder();

            var g = ScriptableObject.CreateInstance<StarterGraph>();
            AssetDatabase.CreateAsset(g, SamplePath);

            // Typed parameter: seeded into the StarterContext on run (InitFromGraph) — gates the Left option.
            g.AddParameter(ParameterData.Bool(StarterContextKeys.Flag, true));

            // Example action (BaseAction) + example condition (BaseCondition), stored as sub-assets.
            var introLog = Sub<LogAction>(g, "IntroLog"); introLog.Message = "Intro";
            var leftLog  = Sub<LogAction>(g, "LeftLog");  leftLog.Message  = "Took Left";
            var rightLog = Sub<LogAction>(g, "RightLog"); rightLog.Message = "Took Right";
            var flagCond = Sub<BoolCondition>(g, "FlagCond"); flagCond.ParameterKey = StarterContextKeys.Flag; flagCond.ExpectedValue = true;
            var toggleFlag = Sub<ToggleBoolAction>(g, "ToggleFlag"); toggleFlag.ParameterKey = StarterContextKeys.Flag;

            var start  = new StartNodeData            { Id = NewId(), NodeType = StartNodeData.NodeTypeId,            Position = new Vector2(0,    0) };
            var intro  = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Intro", Position = new Vector2(240,  0) };
            intro.OnEnterActions.Add(introLog);

            var choice = new ChoiceNodeData { Id = NewId(), NodeType = ChoiceNodeData.NodeTypeId, Position = new Vector2(480, 0) };
            var left   = new StarterChoice { Id = NewId(), Label = "Left (flag)", Condition = flagCond };
            var right  = new StarterChoice { Id = NewId(), Label = "Right" };
            choice.Choices.Add(left);
            choice.Choices.Add(right);

            var aNode = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Left",  Position = new Vector2(740, -120) }; aNode.OnEnterActions.Add(leftLog);
            aNode.OnExitActions.Add(toggleFlag);
            var bNode = new StarterStatementNodeData { Id = NewId(), NodeType = StarterStatementNodeData.NodeTypeId, Label = "Right", Position = new Vector2(740,  120) }; bNode.OnEnterActions.Add(rightLog);
            var end   = new EndNodeData { Id = NewId(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, Position = new Vector2(1000, 0) };

            g.AddNode(start); g.AddNode(intro); g.AddNode(choice); g.AddNode(aNode); g.AddNode(bNode); g.AddNode(end);
            g.EntryNodeId = start.Id;

            g.AddEdge(Edge(start.Id,  intro.Id,  "out"));
            g.AddEdge(Edge(intro.Id,  choice.Id, "out"));
            g.AddEdge(Edge(choice.Id, aNode.Id,  left.Id));   // choice edges route by the choice's GUID = its port name
            g.AddEdge(Edge(choice.Id, bNode.Id,  right.Id));
            g.AddEdge(Edge(aNode.Id,  end.Id,    "out"));
            g.AddEdge(Edge(bNode.Id,  end.Id,    "out"));

            EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = g;
            EditorGUIUtility.PingObject(g);
            Debug.Log($"[StarterGraph] Minimal sample created: {SamplePath}. Open it, press Run → it pauses at the " +
                      "Choice (Left is gated by the Flag bool parameter, Right is always available). Taking Left " +
                      "toggles the flag (ToggleBoolAction) so the gate flips on GoBack. It exercises the template's " +
                      "pattern to copy: graph + typed context + node + action + condition + choice.");
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
