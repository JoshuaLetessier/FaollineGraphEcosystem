using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.StarterGraph.Editor;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>US2 — window execution loop: pause at Choice, Choose routes, Continue, EndReason log.</summary>
    [TestFixture]
    public class StarterWindowExecutionTests
    {
        private StarterGraphEditorWindow _window;

        [SetUp]    public void SetUp()    => _window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_window);

        // Start → Choice(Left→A, Right→B) → A/B → End ; Left gated by an int condition (score>=3)
        private static StarterGraph BuildChoiceGraph(out StarterIntCondition cond)
        {
            cond = ScriptableObject.CreateInstance<StarterIntCondition>();
            cond.ParameterKey = StarterContextKeys.Score; cond.Operator = ComparisonOperator.GreaterOrEqual; cond.ExpectedValue = 3;
            var setScore = ScriptableObject.CreateInstance<StarterSetIntAction>(); setScore.ParameterKey = StarterContextKeys.Score; setScore.Value = 5;

            var g = ScriptableObject.CreateInstance<StarterGraph>();
            g.AddParameter(new ParameterData { Key = StarterContextKeys.Score, Type = ParameterType.Int, DefaultValue = "0" });
            var start  = new StartNodeData            { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var setup  = new StarterStatementNodeData { Id = "u", NodeType = StarterStatementNodeData.NodeTypeId };
            setup.OnEnterActions.Add(setScore);
            var choice = new ChoiceNodeData           { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            choice.Choices.Add(new StarterChoice { Id = "left",  Label = "Left",  Condition = cond });
            choice.Choices.Add(new StarterChoice { Id = "right", Label = "Right" });
            var a = new StarterStatementNodeData { Id = "a", NodeType = StarterStatementNodeData.NodeTypeId, Label = "A" };
            var b = new StarterStatementNodeData { Id = "b", NodeType = StarterStatementNodeData.NodeTypeId, Label = "B" };
            var end = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled };
            g.AddNode(start); g.AddNode(setup); g.AddNode(choice); g.AddNode(a); g.AddNode(b); g.AddNode(end);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e0", FromNodeId = "s", ToNodeId = "u", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "u", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "eL", FromNodeId = "c", ToNodeId = "a", PortName = "left" });
            g.AddEdge(new BaseEdgeData { Id = "eR", FromNodeId = "c", ToNodeId = "b", PortName = "right" });
            g.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "a", ToNodeId = "e", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "b", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [Test]
        public void Run_PausesAtChoice_BothOffered_ChooseRoutesAndResumes()
        {
            var g = BuildChoiceGraph(out var cond);
            try
            {
                _window.ExecuteGraph(g);
                Assert.IsTrue(_window.IsWaitingForChoice, "Run must pause at the Choice node");
                Assert.AreEqual(2, _window.AvailableChoices.Count, "score=5 → Left (>=3) and Right both available");

                _window.Choose("left");
                Assert.IsFalse(_window.IsWaitingForChoice, "Choose must resume execution");
            }
            finally { Object.DestroyImmediate(cond); Object.DestroyImmediate(g); }
        }

        [Test]
        public void Continue_AfterGoBack_RePausesAtChoice()
        {
            var g = BuildChoiceGraph(out var cond);
            try
            {
                _window.ExecuteGraph(g);
                Assert.IsTrue(_window.IsWaitingForChoice);
                _window.GoBack();
                Assert.IsFalse(_window.IsWaitingForChoice);
                _window.Continue();
                Assert.IsTrue(_window.IsWaitingForChoice, "Continue must re-advance and pause at the Choice again");
            }
            finally { Object.DestroyImmediate(cond); Object.DestroyImmediate(g); }
        }

        [Test]
        public void Choose_RoutesToEnd_LogsConfiguredEndReason()
        {
            var g = BuildChoiceGraph(out var cond);
            try
            {
                _window.ExecuteGraph(g);
                LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Graph ended: Cancelled"));
                _window.Choose("right");
            }
            finally { Object.DestroyImmediate(cond); Object.DestroyImmediate(g); }
        }
    }
}
