using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// US2 — cascade: MarkCompleted adds to the completed-set and re-evaluates, unlocking dependents;
    /// re-marking is a no-op. DAG A,B→C.
    /// </summary>
    public class ReactiveCascadeTests
    {
        private BaseGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            _graph.AddNode(new StatementNodeData { Id = "A", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "B", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "C", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "A", ToNodeId = "C" });
            _graph.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "B", ToNodeId = "C" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void MarkingLastPrerequisite_UnlocksDependent()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");

            e.MarkCompleted("A");
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("C"), "C still needs B.");

            e.MarkCompleted("B");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("C"));
            Assert.AreEqual(ReactiveNodeState.Completed, e.GetState("A"));
            Assert.AreEqual(ReactiveNodeState.Completed, e.GetState("B"));
        }

        [Test]
        public void MarkCompleted_RecordsIdInCompletedSet()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            e.MarkCompleted("A");
            Assert.IsTrue(ctx.CollectionContains("completed", "A"));
        }

        [Test]
        public void ReMarkingCompletedNode_IsNoOp()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            e.MarkCompleted("A");
            e.MarkCompleted("A");
            Assert.AreEqual(1, ctx.CollectionCount("completed"));
            Assert.AreEqual(ReactiveNodeState.Completed, e.GetState("A"));
        }
    }
}
