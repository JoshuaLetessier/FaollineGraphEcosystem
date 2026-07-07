using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.StarterGraph.Editor;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>Window execution loop: pause at Choice, Choose routes, Continue after GoBack, EndReason log.</summary>
    [TestFixture]
    public class StarterWindowExecutionTests
    {
        private StarterGraphEditorWindow _window;
        private ParameterName _flag;

        [SetUp]    public void SetUp()    => _window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_window);
            if (_flag != null) Object.DestroyImmediate(_flag);
        }

        // Start → Setup → Choice(Left gated by the Flag bool param, Right always) → A/B → End.
        // The gating value comes from the Flag ParameterName's default (true), seeded via InitFromGraph because
        // the choice condition references it — so no context-mutating action is needed.
        private StarterGraph BuildChoiceGraph(out BoolCondition cond)
        {
            _flag = ParameterName.Bool(StarterContextKeys.Flag, true);
            cond = ScriptableObject.CreateInstance<BoolCondition>();
            cond.Parameter = _flag; cond.ExpectedValue = true;

            var g = ScriptableObject.CreateInstance<StarterGraph>();

            var start  = new StartNodeData            { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var setup  = new StarterStatementNodeData { Id = "u", NodeType = StarterStatementNodeData.NodeTypeId };
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
                Assert.AreEqual(2, _window.AvailableChoices.Count, "Flag=true → Left (gated) and Right both available");

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
