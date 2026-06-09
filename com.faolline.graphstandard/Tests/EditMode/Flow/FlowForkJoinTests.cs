using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>US1 + US2 — fork (activate all valid edges) and join (k-of-N rendezvous, default AND).</summary>
    public class FlowForkJoinTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown() { foreach (var o in _so) Object.DestroyImmediate(o); _so.Clear(); }

        private BaseGraph NewGraph() { var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g); return g; }
        private static StatementNodeData Node(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };

        [Test]
        public void Fork_FiresAllSuccessors()
        {
            var g = NewGraph();
            g.AddNode(Node("cast")); g.AddNode(Node("a")); g.AddNode(Node("b")); g.AddNode(Node("c"));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "cast", ToNodeId = "a" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "cast", ToNodeId = "b" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "cast", ToNodeId = "c" });

            var flow = new FlowRunner(g, new BaseContext());
            var fired = new List<string>(); flow.OnNodeFired += fired.Add;
            flow.Fire("cast");

            CollectionAssert.AreEquivalent(new[] { "cast", "a", "b", "c" }, fired);
        }

        [Test]
        public void Fork_FalseConditionEdge_DoesNotFireTarget()
        {
            var g = NewGraph();
            g.AddNode(Node("cast")); g.AddNode(Node("a")); g.AddNode(Node("b"));
            var cond = ScriptableObject.CreateInstance<FlowBoolCondition>(); cond.Key = "open"; _so.Add(cond);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "cast", ToNodeId = "a", Condition = cond });   // false
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "cast", ToNodeId = "b" });

            var flow = new FlowRunner(g, new BaseContext());
            flow.Fire("cast");

            Assert.IsFalse(flow.HasFired("a"));
            Assert.IsTrue(flow.HasFired("b"));
        }

        [Test]
        public void Chain_Cascades()
        {
            var g = NewGraph();
            g.AddNode(Node("a")); g.AddNode(Node("b")); g.AddNode(Node("c"));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "b" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "b", ToNodeId = "c" });

            var flow = new FlowRunner(g, new BaseContext());
            flow.Fire("a");

            Assert.IsTrue(flow.HasFired("a") && flow.HasFired("b") && flow.HasFired("c"));
        }

        [Test]
        public void AndJoin_FiresOnlyAfterAllPredecessors()
        {
            var g = NewGraph();
            g.AddNode(Node("a")); g.AddNode(Node("b")); g.AddNode(Node("j"));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "j" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "b", ToNodeId = "j" });

            var flow = new FlowRunner(g, new BaseContext());
            int jFires = 0; flow.OnNodeFired += id => { if (id == "j") jFires++; };

            flow.Fire("a");
            Assert.IsFalse(flow.HasFired("j"), "j needs both a and b (AND).");
            flow.Fire("b");
            Assert.IsTrue(flow.HasFired("j"));
            Assert.AreEqual(1, jFires);
        }

        [Test]
        public void ForkReconvergingAtJoin_FiresJoinOnce()
        {
            var g = NewGraph();
            foreach (var id in new[] { "cast", "d", "e", "f", "cd" }) g.AddNode(Node(id));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "cast", ToNodeId = "d" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "cast", ToNodeId = "e" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "cast", ToNodeId = "f" });
            g.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "d", ToNodeId = "cd" });
            g.AddEdge(new BaseEdgeData { Id = "e5", FromNodeId = "e", ToNodeId = "cd" });
            g.AddEdge(new BaseEdgeData { Id = "e6", FromNodeId = "f", ToNodeId = "cd" });

            var flow = new FlowRunner(g, new BaseContext());
            int cdFires = 0; flow.OnNodeFired += id => { if (id == "cd") cdFires++; };
            flow.Fire("cast");

            Assert.IsTrue(flow.HasFired("cd"));
            Assert.AreEqual(1, cdFires);
        }

        [Test]
        public void OrJoin_ThresholdOne_FiresOnFirstArrival()
        {
            var g = NewGraph();
            g.AddNode(Node("a")); g.AddNode(Node("b")); g.AddNode(Node("j"));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "j" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "b", ToNodeId = "j" });

            var flow = new FlowRunner(g, new BaseContext(), joinThresholds: new Dictionary<string, int> { ["j"] = 1 });
            flow.Fire("a");

            Assert.IsTrue(flow.HasFired("j"), "OR-join fires on the first arrival.");
        }
    }
}
