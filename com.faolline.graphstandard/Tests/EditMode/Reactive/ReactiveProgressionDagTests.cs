using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// US4 + SC-006 — durable / reversible re-pass over a game-like multi-tier progression DAG:
    /// Crank,RepairLadder → Gate ; Gate,Simon → RegionDone. Un-completing a node and re-evaluating yields
    /// the smaller satisfied set (re-pass, no side-effects); derivation is idempotent.
    /// </summary>
    public class ReactiveProgressionDagTests
    {
        private BaseGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            foreach (var id in new[] { "Crank", "RepairLadder", "Gate", "Simon", "RegionDone" })
                _graph.AddNode(new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "Crank",        ToNodeId = "Gate" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "RepairLadder", ToNodeId = "Gate" });
            _graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "Gate",         ToNodeId = "RegionDone" });
            _graph.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "Simon",        ToNodeId = "RegionDone" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void MultiTierDag_UnlocksTierByTier()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");

            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("Crank"));
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("RepairLadder"));
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("Simon"));
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("Gate"));
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("RegionDone"));

            e.MarkCompleted("Crank");
            e.MarkCompleted("RepairLadder");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("Gate"), "Gate unlocks after both prereqs.");
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("RegionDone"), "RegionDone still needs Gate+Simon.");

            e.MarkCompleted("Gate");
            e.MarkCompleted("Simon");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("RegionDone"));
        }

        [Test]
        public void UnComplete_ThenReevaluate_RePassesToSmallerSet()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            e.MarkCompleted("Crank");
            e.MarkCompleted("RepairLadder");
            e.MarkCompleted("Gate");
            e.MarkCompleted("Simon");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("RegionDone"));

            // Un-complete Gate (e.g. a step-back / restore shrinks the completed-set) and re-evaluate.
            ctx.RemoveFromCollection("completed", "Gate");
            e.Reevaluate();

            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("Gate"), "Gate re-derives to Available (prereqs still done).");
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("RegionDone"), "RegionDone re-locks — re-pass, not undo.");
        }

        [Test]
        public void Reevaluate_IsIdempotent()
        {
            var ctx = new BaseContext();
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            e.MarkCompleted("Crank");

            var before = new System.Collections.Generic.List<string>(e.AvailableNodeIds);
            e.Reevaluate();
            e.Reevaluate();
            var after = new System.Collections.Generic.List<string>(e.AvailableNodeIds);

            CollectionAssert.AreEquivalent(before, after);
        }
    }
}
