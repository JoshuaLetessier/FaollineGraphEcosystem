using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// Slice 7 — the symmetric <c>OnNodeLocked</c> re-lock event: fires on a backward transition to Locked
    /// during Reevaluate and once per initially-Locked node during Start(), never on an unchanged node.
    /// </summary>
    public class ReactiveRelockEventTests
    {
        private BaseGraph _graph;   // A, B → D (D requires both by default; k configurable)

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            foreach (var id in new[] { "A", "B", "D" })
                _graph.AddNode(new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "A", ToNodeId = "D" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "B", ToNodeId = "D" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void OnNodeLocked_FiresOnBackwardTransition()
        {
            var ctx = new BaseContext();
            var eval = new ReactiveEvaluator(_graph, ctx, "completed",
                new Dictionary<string, int> { ["D"] = 2 });
            eval.MarkCompleted("A");
            eval.MarkCompleted("B");
            Assert.AreEqual(ReactiveNodeState.Available, eval.GetState("D"));

            int dLocked = 0;
            eval.OnNodeLocked += id => { if (id == "D") dLocked++; };

            ctx.RemoveFromCollection("completed", "B");   // drop below threshold
            eval.Reevaluate();

            Assert.AreEqual(ReactiveNodeState.Locked, eval.GetState("D"));
            Assert.AreEqual(1, dLocked, "re-lock event fires once on the backward transition");
        }

        [Test]
        public void OnNodeLocked_FiresForInitiallyLockedNodes_AtStart()
        {
            var ctx = new BaseContext();
            var eval = new ReactiveEvaluator(_graph, ctx, "completed");   // nothing completed → D Locked, A/B Available

            var locked = new List<string>();
            eval.OnNodeLocked += locked.Add;
            eval.Start();

            Assert.Contains("D", locked, "initially-Locked node emits the re-lock event on Start");
            CollectionAssert.DoesNotContain(locked, "A", "an Available node must not emit re-lock");
            CollectionAssert.DoesNotContain(locked, "B", "an Available node must not emit re-lock");
        }

        [Test]
        public void OnNodeLocked_DoesNotFire_WhenStateUnchanged()
        {
            var ctx = new BaseContext();
            var eval = new ReactiveEvaluator(_graph, ctx, "completed");
            eval.Start();

            int aLocked = 0;
            eval.OnNodeLocked += id => { if (id == "A") aLocked++; };

            eval.MarkCompleted("A");   // A stays Available (no prereqs) — its state does not change to Locked
            eval.Reevaluate();

            Assert.AreEqual(0, aLocked, "no re-lock for a node whose state is unchanged");
        }
    }
}
