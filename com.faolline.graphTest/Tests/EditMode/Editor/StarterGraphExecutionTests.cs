using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;
using Faolline.StarterGraph;
using Faolline.StarterGraph.Editor;
using StarterGraphAsset = Faolline.StarterGraph.StarterGraph;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class StarterGraphExecutionTests
    {
        private StarterGraphAsset BuildLinearGraph()
        {
            var graph = ScriptableObject.CreateInstance<StarterGraphAsset>();

            var start = new StartNodeData   { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var stmt  = new StarterStatementNodeData { Id = "stmt", NodeType = StarterStatementNodeData.NodeTypeId, Label = "hello" };
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
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
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
                Assert.AreEqual(StarterStatementNodeData.NodeTypeId,   visitedTypes[1]);
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
        private static StarterGraphAsset BuildChoiceGraph(BaseCondition leftCondition = null)
        {
            var graph = ScriptableObject.CreateInstance<StarterGraphAsset>();

            var start  = new StartNodeData       { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var choice = new ChoiceNodeData       { Id = "choice", NodeType = ChoiceNodeData.NodeTypeId };
            var a      = new StarterStatementNodeData { Id = "a", NodeType = StarterStatementNodeData.NodeTypeId, Label = "A" };
            var b      = new StarterStatementNodeData { Id = "b", NodeType = StarterStatementNodeData.NodeTypeId, Label = "B" };
            var end    = new EndNodeData          { Id = "end", NodeType = EndNodeData.NodeTypeId };

            choice.Choices.Add(new StarterChoice { Id = "left",  Label = "Left",  Condition = leftCondition });
            choice.Choices.Add(new StarterChoice { Id = "right", Label = "Right" });

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
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
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
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
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
        public void SubGraphNode_RunsChildThenResumesParent()
        {
            var child  = ScriptableObject.CreateInstance<StarterGraphAsset>();
            var parent = ScriptableObject.CreateInstance<StarterGraphAsset>();
            try
            {
                // child: Start → Statement("inner") → End
                child.AddNode(new StartNodeData         { Id = "cs",    NodeType = StartNodeData.NodeTypeId });
                child.AddNode(new StarterStatementNodeData { Id = "cstmt", NodeType = StarterStatementNodeData.NodeTypeId, Label = "inner" });
                child.AddNode(new EndNodeData           { Id = "ce",    NodeType = EndNodeData.NodeTypeId });
                child.EntryNodeId = "cs";
                child.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "cs",    ToNodeId = "cstmt", PortName = "out" });
                child.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "cstmt", ToNodeId = "ce",    PortName = "out" });

                // parent: Start → SubGraph(child) → End
                parent.AddNode(new StartNodeData    { Id = "ps", NodeType = StartNodeData.NodeTypeId });
                parent.AddNode(new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child });
                parent.AddNode(new EndNodeData      { Id = "pe", NodeType = EndNodeData.NodeTypeId });
                parent.EntryNodeId = "ps";
                parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "ps", ToNodeId = "sg", PortName = "out" });
                parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "sg", ToNodeId = "pe", PortName = "out" });

                var visited = new List<string>();
                var runner  = new BaseRunner();
                runner.OnNodeEntered += n => visited.Add(n.Id);
                runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());
                int guard = 0;
                while (runner.State == RunnerState.NodeReady && guard++ < 100) runner.Proceed();

                Assert.AreEqual(RunnerState.Ended, runner.State, "Parent must complete after the sub-graph returns");
                Assert.Contains("cstmt", visited, "The child graph's node must be visited inside the sub-graph");
                Assert.Contains("pe", visited, "Execution must resume to the parent End after the sub-graph");
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SubGraphNode_NullTarget_HaltsAsStuck()
        {
            var parent = ScriptableObject.CreateInstance<StarterGraphAsset>();
            try
            {
                parent.AddNode(new StartNodeData    { Id = "ps", NodeType = StartNodeData.NodeTypeId });
                parent.AddNode(new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId }); // TargetGraph null
                parent.AddNode(new EndNodeData      { Id = "pe", NodeType = EndNodeData.NodeTypeId });
                parent.EntryNodeId = "ps";
                parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "ps", ToNodeId = "sg", PortName = "out" });
                parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "sg", ToNodeId = "pe", PortName = "out" });

                bool stuck = false;
                var runner = new BaseRunner();
                runner.OnStuck += () => stuck = true;

                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(@"TargetGraph is null"));

                runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());
                int guard = 0;
                while (runner.State == RunnerState.NodeReady && !stuck && guard++ < 100) runner.Proceed();

                Assert.IsTrue(stuck, "A null sub-graph target must halt via OnStuck, not throw");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void ExecuteGraph_EndReasonCancelled_LogsCancelled()
        {
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
            var graph  = ScriptableObject.CreateInstance<StarterGraphAsset>();
            try
            {
                var start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var end   = new EndNodeData   { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled };
                graph.AddNode(start);
                graph.AddNode(end);
                graph.EntryNodeId = "s";
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });

                LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(@"Graph ended: Cancelled"));

                window.ExecuteGraph(graph);
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ExecuteGraph_NullGraph_LogsError()
        {
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
            try
            {
                LogAssert.Expect(LogType.Error,
                    "[StarterGraph] No graph loaded. Open a StarterGraph asset first.");
                window.ExecuteGraph(null);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ExecuteGraph_NoEntryNodeId_LogsError()
        {
            var window = ScriptableObject.CreateInstance<StarterGraphEditorWindow>();
            var graph  = ScriptableObject.CreateInstance<StarterGraphAsset>();
            try
            {
                // Graph has no EntryNodeId set
                LogAssert.Expect(LogType.Error,
                    "[StarterGraph] Graph has no entry node set. Add a Start node and save before running.");
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
