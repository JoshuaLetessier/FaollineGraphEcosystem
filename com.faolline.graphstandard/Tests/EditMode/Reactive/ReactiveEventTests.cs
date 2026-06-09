using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// US3 — events: Start emits the initial Available/Completed set; MarkCompleted emits transitions
    /// (no spurious, idempotent). DAG A,B→C.
    /// </summary>
    public class ReactiveEventTests
    {
        private BaseGraph _graph;
        private List<string> _available;
        private List<string> _completed;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            _graph.AddNode(new StatementNodeData { Id = "A", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "B", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "C", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "A", ToNodeId = "C" });
            _graph.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "B", ToNodeId = "C" });
            _available = new List<string>();
            _completed = new List<string>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        private ReactiveEvaluator Subscribe(BaseContext ctx)
        {
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            e.OnNodeAvailable += id => _available.Add(id);
            e.OnNodeCompleted += id => _completed.Add(id);
            return e;
        }

        [Test]
        public void Start_EmitsAvailable_ForInitiallyAvailable_NotForLocked()
        {
            var e = Subscribe(new BaseContext());
            e.Start();
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, _available);
            CollectionAssert.IsEmpty(_completed);
            Assert.IsFalse(_available.Contains("C"));
        }

        [Test]
        public void Start_EmitsCompleted_ForAlreadyCompletedNode()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "A");
            var e = Subscribe(ctx);
            e.Start();
            Assert.Contains("A", _completed);
        }

        [Test]
        public void MarkCompleted_EmitsCompletedThenUnlocksDependent()
        {
            var ctx = new BaseContext();
            var e = Subscribe(ctx);
            e.Start();
            _available.Clear();
            _completed.Clear();

            e.MarkCompleted("A");                 // A completed; C still locked
            Assert.Contains("A", _completed);
            Assert.IsFalse(_available.Contains("C"));

            e.MarkCompleted("B");                 // B completed; C unlocks
            Assert.Contains("B", _completed);
            Assert.AreEqual(1, _available.FindAll(x => x == "C").Count, "C becomes Available exactly once.");
        }

        [Test]
        public void ReMark_EmitsNothing()
        {
            var ctx = new BaseContext();
            var e = Subscribe(ctx);
            e.Start();
            e.MarkCompleted("A");
            _available.Clear();
            _completed.Clear();

            e.MarkCompleted("A");                 // already completed

            CollectionAssert.IsEmpty(_available);
            CollectionAssert.IsEmpty(_completed);
        }
    }
}
