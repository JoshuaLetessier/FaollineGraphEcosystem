using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class BaseRunnerCurrentNodeTests
    {
        private BaseGraph _graph;
        private BaseRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_graph);
        }

        private static BaseGraph BuildLinear(out StartNodeData start, out EndNodeData end)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            end   = new EndNodeData   { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(start);
            g.AddNode(end);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [Test]
        public void CurrentNode_BeforeStart_IsNull()
        {
            var runner = new BaseRunner();
            Assert.IsNull(runner.CurrentNode,
                "CurrentNode must be null before Start is called");
        }

        [Test]
        public void CurrentNode_AfterStart_ReturnsEntryNode()
        {
            var graph = BuildLinear(out var start, out _);
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());

            Assert.IsNotNull(runner.CurrentNode,
                "CurrentNode must not be null after Start");
            Assert.AreEqual(start.Id, runner.CurrentNode.Id,
                "CurrentNode must match the entry node immediately after Start");

            Object.DestroyImmediate(graph);
        }

        [Test]
        public void CurrentNode_AfterProceed_AdvancesToNextNode()
        {
            var graph = BuildLinear(out _, out var end);
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();

            Assert.AreEqual(end.Id, runner.CurrentNode?.Id,
                "CurrentNode must advance to the end node after Proceed");

            Object.DestroyImmediate(graph);
        }

        [Test]
        public void CurrentNode_AfterEnded_ReturnsLastNode()
        {
            var graph = BuildLinear(out _, out var end);
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed(); // first Proceed enters the End node (still NodeReady)
            runner.Proceed(); // second Proceed finalizes the End node -> Ended

            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.AreEqual(end.Id, runner.CurrentNode?.Id,
                "CurrentNode must still return the last visited node when State == Ended");

            Object.DestroyImmediate(graph);
        }
    }
}
