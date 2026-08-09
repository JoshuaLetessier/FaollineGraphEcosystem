using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>DialogueGraphBuilder.AddSubGraph — a dialogue-to-dialogue jump, mirroring graphstandard's AddSubGraph.</summary>
    public class DialogueGraphBuilderSubGraphTests
    {
        readonly List<Object> _created = new List<Object>();
        DialogueGraph Track(DialogueGraph g) { _created.Add(g); return g; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void AddSubGraph_SetsNodeTypeAndTargetGraph()
        {
            var target = ScriptableObject.CreateInstance<DialogueGraph>();
            _created.Add(target);

            var b = new DialogueGraphBuilder();
            var hi = b.AddLine("guardian", "Bonjour").AsEntry();
            var sub = b.AddSubGraph("Commerce", target);
            hi.To(sub);

            var g = Track(b.Build());

            var subNode = g.Nodes.OfType<SubGraphNodeData>().Single();
            Assert.AreEqual(SubGraphNodeData.NodeTypeId, subNode.NodeType);
            Assert.AreEqual(target, subNode.TargetGraph);
            Assert.AreEqual("Commerce", subNode.Title);
        }

        [Test]
        public void AddSubGraph_WithNullTarget_IsAllowed()
        {
            var b = new DialogueGraphBuilder();
            var sub = b.AddSubGraph("Unresolved").AsEntry();
            var end = b.AddEnd();
            sub.To(end);

            var g = Track(b.Build());

            var subNode = g.Nodes.OfType<SubGraphNodeData>().Single();
            Assert.IsNull(subNode.TargetGraph);
        }

        [Test]
        public void AddSubGraph_WiresIntoExistingEdgeMachinery()
        {
            var b = new DialogueGraphBuilder();
            var sub = b.AddSubGraph("Jump").AsEntry();
            var end = b.AddEnd();
            sub.To(end);

            var g = Track(b.Build());

            Assert.AreEqual(1, g.Edges.Count);
            var subNode = g.Nodes.OfType<SubGraphNodeData>().Single();
            var endNode = g.Nodes.OfType<EndNodeData>().Single();
            Assert.AreEqual(subNode.Id, g.Edges[0].FromNodeId);
            Assert.AreEqual(endNode.Id, g.Edges[0].ToNodeId);
        }
    }
}
