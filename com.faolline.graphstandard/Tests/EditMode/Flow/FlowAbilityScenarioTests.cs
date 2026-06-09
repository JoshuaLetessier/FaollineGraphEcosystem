using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>US4 — firing runs node enter-actions and edge conditions gate; full ability-cast scenario.</summary>
    public class FlowAbilityScenarioTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown() { foreach (var o in _so) Object.DestroyImmediate(o); _so.Clear(); }

        private BaseGraph NewGraph() { var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g); return g; }
        private static StatementNodeData Node(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };

        [Test]
        public void Firing_RunsEnterAction_MutatesContext()
        {
            var g = NewGraph();
            var effect = Node("effect");
            var act = ScriptableObject.CreateInstance<FlowAddToCollectionAction>(); act.Key = "applied"; act.Item = "burn"; _so.Add(act);
            effect.OnEnterActions.Add(act);
            g.AddNode(effect);

            var ctx = new BaseContext();
            var flow = new FlowRunner(g, ctx);
            flow.Fire("effect");

            Assert.IsTrue(ctx.CollectionContains("applied", "burn"));
        }

        [Test]
        public void AbilityCast_ForkJoin_ResolvesOnce_CooldownLast()
        {
            var g = NewGraph();
            foreach (var id in new[] { "cast", "damage", "debuff", "vfx", "cooldown" }) g.AddNode(Node(id));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "cast", ToNodeId = "damage" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "cast", ToNodeId = "debuff" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "cast", ToNodeId = "vfx" });
            g.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "damage",  ToNodeId = "cooldown" });
            g.AddEdge(new BaseEdgeData { Id = "e5", FromNodeId = "debuff",  ToNodeId = "cooldown" });
            g.AddEdge(new BaseEdgeData { Id = "e6", FromNodeId = "vfx",     ToNodeId = "cooldown" });

            var flow = new FlowRunner(g, new BaseContext());
            var order = new List<string>(); flow.OnNodeFired += order.Add;
            flow.Fire("cast");

            CollectionAssert.AreEquivalent(new[] { "cast", "damage", "debuff", "vfx", "cooldown" }, order);
            Assert.AreEqual("cooldown", order[order.Count - 1], "cooldown fires last (after the join).");
            Assert.AreEqual(1, order.FindAll(x => x == "cooldown").Count);
        }

        [Test]
        public void ConditionalEdge_GatesPropagation()
        {
            var g = NewGraph();
            g.AddNode(Node("a")); g.AddNode(Node("b"));
            var cond = ScriptableObject.CreateInstance<FlowBoolCondition>(); cond.Key = "go"; _so.Add(cond);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "b", Condition = cond });

            var ctx = new BaseContext();
            var flow = new FlowRunner(g, ctx);
            flow.Fire("a");
            Assert.IsFalse(flow.HasFired("b"), "gated off while the condition is false.");

            ctx.Set<bool>("go", true);
            flow.Reset();
            flow.Fire("a");
            Assert.IsTrue(flow.HasFired("b"), "propagates once the condition is true.");
        }
    }
}
