using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestGraphExecutionTests
    {
        private TestGraph BuildLinearGraph()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();

            var start = new StartNodeData   { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var stmt  = new TestStatementNodeData { Id = "stmt", NodeType = TestStatementNodeData.NodeTypeId, Label = "hello" };
            var end   = new EndNodeData     { Id = "end",   NodeType = EndNodeData.NodeTypeId };

            graph.AddNode(start);
            graph.AddNode(stmt);
            graph.AddNode(end);
            graph.EntryNodeId = "start";

            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "stmt", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "stmt",  ToNodeId = "end",  PortName = "out" });

            return graph;
        }

        [Test]
        public void ExecuteGraph_LinearChain_LogsAllThreeNodes()
        {
            var window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            var graph  = BuildLinearGraph();
            try
            {
                var visitedTypes = new List<string>();
                // Capture via LogAssert isn't straightforward for multiple; use BaseRunner directly
                var context  = new BaseContext();
                var registry = new NodeExecutorRegistry();
                var runner   = new BaseRunner();

                runner.OnNodeEntered += node => visitedTypes.Add(node.NodeType);

                runner.Start(graph, context, registry);
                while (runner.State == RunnerState.NodeReady)
                    runner.Proceed();

                Assert.AreEqual(3, visitedTypes.Count,
                    "A Start→Statement→End chain must visit exactly 3 nodes");
                Assert.AreEqual(StartNodeData.NodeTypeId,           visitedTypes[0]);
                Assert.AreEqual(TestStatementNodeData.NodeTypeId,   visitedTypes[1]);
                Assert.AreEqual(EndNodeData.NodeTypeId,             visitedTypes[2]);
                Assert.AreEqual(RunnerState.Ended, runner.State);
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }

        // Start → Choice(Left→A, Right→B) → A→End, B→End
        private static TestGraph BuildChoiceGraph(BaseCondition leftCondition = null)
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();

            var start  = new StartNodeData       { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var choice = new ChoiceNodeData       { Id = "choice", NodeType = ChoiceNodeData.NodeTypeId };
            var a      = new TestStatementNodeData { Id = "a", NodeType = TestStatementNodeData.NodeTypeId, Label = "A" };
            var b      = new TestStatementNodeData { Id = "b", NodeType = TestStatementNodeData.NodeTypeId, Label = "B" };
            var end    = new EndNodeData          { Id = "end", NodeType = EndNodeData.NodeTypeId };

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
        public void ExecuteGraph_ReachingChoiceNode_PausesAndLogsWaiting()
        {
            var window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            var graph  = BuildChoiceGraph();
            try
            {
                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(@"Waiting for choice at node: choice"));

                window.ExecuteGraph(graph);

                Assert.IsTrue(window.IsWaitingForChoice,
                    "Execution must pause and set the waiting flag at a Choice node");
                Assert.AreSame(graph.Nodes[1], window.WaitingChoiceNode,
                    "WaitingChoiceNode must be the Choice node reached");
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ChoiceNode_WithAllFailingConditions_HaltsAsStuck()
        {
            var falseCond = ScriptableObject.CreateInstance<TestAlwaysFalseCondition>();
            var window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            // Both choices unavailable: left fails, right also gated false.
            var graph  = BuildChoiceGraph(leftCondition: falseCond);
            var rightChoice = ((ChoiceNodeData)graph.Nodes[1]).Choices[1];
            rightChoice.Condition = falseCond;
            try
            {
                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(@"runner is stuck"));

                window.ExecuteGraph(graph);

                Assert.IsFalse(window.IsWaitingForChoice,
                    "When no choice passes its condition, execution must not wait — it halts as stuck");
            }
            finally
            {
                Object.DestroyImmediate(falseCond);
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ExecuteGraph_NullGraph_LogsError()
        {
            var window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            try
            {
                LogAssert.Expect(LogType.Error,
                    "[GraphTest] No graph loaded. Open a TestGraph asset first.");
                window.ExecuteGraph(null);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ExecuteGraph_NoEntryNodeId_LogsError()
        {
            var window = ScriptableObject.CreateInstance<TestGraphEditorWindow>();
            var graph  = ScriptableObject.CreateInstance<TestGraph>();
            try
            {
                // Graph has no EntryNodeId set
                LogAssert.Expect(LogType.Error,
                    "[GraphTest] Graph has no entry node set. Add a Start node and save before running.");
                window.ExecuteGraph(graph);
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }
    }
}
