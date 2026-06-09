using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>US3 — re-pass (re-fire / cycles bounded by the cap) and one-shot (fire once until Reset).</summary>
    public class FlowRePassOneShotTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown() { foreach (var o in _so) Object.DestroyImmediate(o); _so.Clear(); }

        private BaseGraph NewGraph() { var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g); return g; }
        private static StatementNodeData Node(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };

        [Test]
        public void NonOneShot_RefiresEachFire()
        {
            var g = NewGraph(); g.AddNode(Node("a"));
            var flow = new FlowRunner(g, new BaseContext());
            int aFires = 0; flow.OnNodeFired += id => { if (id == "a") aFires++; };
            flow.Fire("a"); flow.Fire("a");
            Assert.AreEqual(2, aFires);
        }

        [Test]
        public void OneShot_FiresOnce_ThenResetReArms()
        {
            var g = NewGraph(); g.AddNode(Node("a"));
            var flow = new FlowRunner(g, new BaseContext(), oneShotNodeIds: new[] { "a" });
            int aFires = 0; flow.OnNodeFired += id => { if (id == "a") aFires++; };

            flow.Fire("a"); flow.Fire("a");
            Assert.AreEqual(1, aFires, "one-shot fires once until reset.");

            flow.Reset();
            flow.Fire("a");
            Assert.AreEqual(2, aFires, "Reset re-arms the one-shot.");
        }

        [Test]
        public void Cycle_IsBounded_ByFireCap_WithWarning()
        {
            var g = NewGraph(); g.AddNode(Node("a")); g.AddNode(Node("b"));
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "b" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "b", ToNodeId = "a" });

            var flow = new FlowRunner(g, new BaseContext(), maxFiresPerPropagation: 10);
            LogAssert.Expect(LogType.Warning,
                "[GraphStandard] FlowRunner exceeded 10 fires in one propagation (possible cycle); stopping.");
            Assert.DoesNotThrow(() => flow.Fire("a"));   // bounded — does not hang
        }
    }
}
