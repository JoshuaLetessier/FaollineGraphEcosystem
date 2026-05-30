using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestGraphEditorWindowSessionTests
    {
        private TestGraphEditorWindow _window;
        private TestGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            _graph  = ScriptableObject.CreateInstance<TestGraph>();

            var start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var end   = new EndNodeData   { Id = "e", NodeType = EndNodeData.NodeTypeId };
            _graph.AddNode(start);
            _graph.AddNode(end);
            _graph.EntryNodeId = "s";
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_window);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void ExecuteGraph_SetsHasActiveSession()
        {
            _window.ExecuteGraph(_graph);

            Assert.IsTrue(_window.HasActiveSession,
                "ExecuteGraph must set HasActiveSession to true after a successful run");
        }

        [Test]
        public void GoBack_WithNoSession_LogsWarning()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"No active session"));
            _window.GoBack();
        }

        [Test]
        public void GoBackToCheckpoint_WithNoSession_LogsWarning()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"No active session"));
            _window.GoBackToCheckpoint();
        }

        [Test]
        public void GoBack_WithSession_DoesNotThrow()
        {
            _window.ExecuteGraph(_graph);

            Assert.DoesNotThrow(() => _window.GoBack(),
                "GoBack must not throw when a session exists");
        }

        [Test]
        public void ExecuteGraph_SecondRun_ResetsSession()
        {
            _window.ExecuteGraph(_graph);
            _window.ExecuteGraph(_graph);

            Assert.IsTrue(_window.HasActiveSession,
                "Second Run must still leave HasActiveSession true");
        }

        // ── Choice selection ──────────────────────────────────────────────────

        // Start → Choice(Left→A, Right→B) → A→End, B→End
        private static TestGraph BuildChoiceGraph(BaseCondition leftCondition = null)
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();

            var start  = new StartNodeData        { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var choice = new ChoiceNodeData        { Id = "choice", NodeType = ChoiceNodeData.NodeTypeId };
            var a      = new TestStatementNodeData { Id = "a", NodeType = TestStatementNodeData.NodeTypeId, Label = "A" };
            var b      = new TestStatementNodeData { Id = "b", NodeType = TestStatementNodeData.NodeTypeId, Label = "B" };
            var end    = new EndNodeData           { Id = "end", NodeType = EndNodeData.NodeTypeId };

            choice.Choices.Add(new TestChoice { Id = "left",  Label = "Left",  Condition = leftCondition });
            choice.Choices.Add(new TestChoice { Id = "right", Label = "Right" });

            graph.AddNode(start);
            graph.AddNode(choice);
            graph.AddNode(a);
            graph.AddNode(b);
            graph.AddNode(end);
            graph.EntryNodeId = "start";

            graph.AddEdge(new BaseEdgeData { Id = "e0", FromNodeId = "start",  ToNodeId = "choice", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "eL", FromNodeId = "choice", ToNodeId = "a",      PortName = "left" });
            graph.AddEdge(new BaseEdgeData { Id = "eR", FromNodeId = "choice", ToNodeId = "b",      PortName = "right" });
            graph.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "a",      ToNodeId = "end",    PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "b",      ToNodeId = "end",    PortName = "out" });

            return graph;
        }

        [Test]
        public void Choose_NotWaiting_LogsNoOpMessage()
        {
            LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(@"No active choice — click Run first"));

            Assert.DoesNotThrow(() => _window.Choose("anything"),
                "Choosing while not paused must not throw");
            Assert.IsFalse(_window.IsWaitingForChoice);
        }

        [Test]
        public void Choose_Left_RoutesToBranchAAndResumes()
        {
            var graph = BuildChoiceGraph();
            try
            {
                _window.ExecuteGraph(graph);
                Assert.IsTrue(_window.IsWaitingForChoice, "Must pause at the choice first");

                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex("Node: graphtest/statement \"A\""));
                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(@"Graph ended"));

                _window.Choose("left");

                Assert.IsFalse(_window.IsWaitingForChoice,
                    "After choosing, execution must resume and clear the waiting flag");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Choose_Right_RoutesToBranchB()
        {
            var graph = BuildChoiceGraph();
            try
            {
                _window.ExecuteGraph(graph);

                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex("Node: graphtest/statement \"B\""));

                _window.Choose("right");

                Assert.IsFalse(_window.IsWaitingForChoice);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void GoBack_WhilePausedAtChoice_ClearsWaitingFlag()
        {
            var graph = BuildChoiceGraph();
            try
            {
                _window.ExecuteGraph(graph);
                Assert.IsTrue(_window.IsWaitingForChoice, "Must be paused at the choice");

                Assert.DoesNotThrow(() => _window.GoBack(),
                    "GoBack while paused at a choice must not throw");

                Assert.IsFalse(_window.IsWaitingForChoice,
                    "GoBack must clear the waiting-for-choice flag (FR-012)");
                Assert.IsNull(_window.WaitingChoiceNode,
                    "GoBack must clear the pending choice node reference");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AvailableChoices_ExcludesFailingCondition()
        {
            var falseCond = ScriptableObject.CreateInstance<TestAlwaysFalseCondition>();
            var graph = BuildChoiceGraph(leftCondition: falseCond);
            try
            {
                _window.ExecuteGraph(graph);

                Assert.IsTrue(_window.IsWaitingForChoice,
                    "Right has no condition, so execution must still pause for selection");
                Assert.AreEqual(1, _window.AvailableChoices.Count,
                    "The choice gated by an always-false condition must be excluded");
                Assert.AreEqual("right", _window.AvailableChoices[0].Id,
                    "Only the passing (unconditioned) choice must remain selectable");
            }
            finally
            {
                Object.DestroyImmediate(falseCond);
                Object.DestroyImmediate(graph);
            }
        }

        // ── Continue (resume after GoBack) ────────────────────────────────────

        [Test]
        public void Continue_NoSession_LogsMessage()
        {
            LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(@"No active session"));

            Assert.DoesNotThrow(() => _window.Continue(),
                "Continue without a session must not throw");
        }

        [Test]
        public void Continue_AfterEndedRun_LogsNothingToContinue()
        {
            _window.ExecuteGraph(_graph); // Start → End, runs to completion

            LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(@"Nothing to continue"));

            _window.Continue();
        }

        [Test]
        public void Continue_AfterGoBackFromChoice_RePausesAtChoice()
        {
            var graph = BuildChoiceGraph();
            try
            {
                _window.ExecuteGraph(graph);
                Assert.IsTrue(_window.IsWaitingForChoice, "Must pause at the choice first");

                _window.GoBack();
                Assert.IsFalse(_window.IsWaitingForChoice, "GoBack clears the pending choice");

                _window.Continue();

                Assert.IsTrue(_window.IsWaitingForChoice,
                    "Continue must re-advance from the restored node and pause again at the Choice node");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Continue_WhilePausedAtChoice_DoesNotAdvance()
        {
            var graph = BuildChoiceGraph();
            try
            {
                _window.ExecuteGraph(graph);
                Assert.IsTrue(_window.IsWaitingForChoice);

                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(@"use Choose, not Continue"));

                _window.Continue();

                Assert.IsTrue(_window.IsWaitingForChoice,
                    "Continue must not bypass a pending choice");
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
