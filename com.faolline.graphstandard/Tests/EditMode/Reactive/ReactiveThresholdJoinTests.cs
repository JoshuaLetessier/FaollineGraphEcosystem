using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// P4 — generic k-of-N threshold join over a node's prerequisites. DAG A,B,C → D (D requires A,B,C).
    /// Covers default AND, OR (k=1), N-of-M, boundaries (k≤0, k>N), unknown-id config, and lifecycle.
    /// </summary>
    public class ReactiveThresholdJoinTests
    {
        private BaseGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            foreach (var id in new[] { "A", "B", "C", "D" })
                _graph.AddNode(new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "A", ToNodeId = "D" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "B", ToNodeId = "D" });
            _graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "C", ToNodeId = "D" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        private static BaseContext WithCompleted(params string[] ids)
        {
            var ctx = new BaseContext();
            foreach (var id in ids) ctx.AddToCollection("completed", id);
            return ctx;
        }

        [Test]
        public void Default_NoConfig_NeedsAllPrerequisites()
        {
            var e = new ReactiveEvaluator(_graph, WithCompleted("A", "B"), "completed");
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("D"));

            var e2 = new ReactiveEvaluator(_graph, WithCompleted("A", "B", "C"), "completed");
            Assert.AreEqual(ReactiveNodeState.Available, e2.GetState("D"));
        }

        [Test]
        public void K2_AvailableAfterAnyTwo()
        {
            var counts = new Dictionary<string, int> { ["D"] = 2 };
            var e = new ReactiveEvaluator(_graph, WithCompleted("A", "B"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("D"));
        }

        [Test]
        public void K2_LockedWithOnlyOne()
        {
            var counts = new Dictionary<string, int> { ["D"] = 2 };
            var e = new ReactiveEvaluator(_graph, WithCompleted("A"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("D"));
        }

        [Test]
        public void K1_IsOr()
        {
            var counts = new Dictionary<string, int> { ["D"] = 1 };
            var e = new ReactiveEvaluator(_graph, WithCompleted("B"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("D"));
        }

        [Test]
        public void KEqualsN_IsAnd()
        {
            var counts = new Dictionary<string, int> { ["D"] = 3 };
            var locked = new ReactiveEvaluator(_graph, WithCompleted("A", "B"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Locked, locked.GetState("D"));

            var avail = new ReactiveEvaluator(_graph, WithCompleted("A", "B", "C"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Available, avail.GetState("D"));
        }

        [Test]
        public void KZeroOrNegative_IsUngated()
        {
            var zero = new ReactiveEvaluator(_graph, new BaseContext(), "completed",
                new Dictionary<string, int> { ["D"] = 0 });
            Assert.AreEqual(ReactiveNodeState.Available, zero.GetState("D"));

            var negative = new ReactiveEvaluator(_graph, new BaseContext(), "completed",
                new Dictionary<string, int> { ["D"] = -5 });
            Assert.AreEqual(ReactiveNodeState.Available, negative.GetState("D"));
        }

        [Test]
        public void KGreaterThanN_NeverAutoAvailable()
        {
            var counts = new Dictionary<string, int> { ["D"] = 4 };   // only 3 prerequisites exist
            var e = new ReactiveEvaluator(_graph, WithCompleted("A", "B", "C"), "completed", counts);
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("D"), "k>N can never be satisfied by prerequisites.");
        }

        [Test]
        public void UnknownIdInConfig_IsIgnored()
        {
            var counts = new Dictionary<string, int> { ["does-not-exist"] = 1 };
            Assert.DoesNotThrow(() => new ReactiveEvaluator(_graph, new BaseContext(), "completed", counts));
        }

        [Test]
        public void Threshold_HonoredByCascadeAndEvents()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed", new Dictionary<string, int> { ["D"] = 2 });
            int dAvailable = 0;
            e.OnNodeAvailable += id => { if (id == "D") dAvailable++; };
            e.Start();

            e.MarkCompleted("A");                                  // 1 of 2 → still Locked
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("D"));
            Assert.AreEqual(0, dAvailable);

            e.MarkCompleted("B");                                  // 2 of 2 → Available, event once
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("D"));
            Assert.AreEqual(1, dAvailable);
        }

        [Test]
        public void Threshold_ReversibleOnStepBack()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed", new Dictionary<string, int> { ["D"] = 2 });
            e.MarkCompleted("A");
            e.MarkCompleted("B");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("D"));

            ctx.RemoveFromCollection("completed", "B");            // drop below threshold
            e.Reevaluate();

            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("D"), "Re-locks below threshold (re-pass).");
        }
    }
}
