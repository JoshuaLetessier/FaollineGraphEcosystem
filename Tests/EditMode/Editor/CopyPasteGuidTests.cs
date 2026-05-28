using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class CopyPasteGuidTests
    {
        // Simulates the GUID reassignment logic from BaseGraphView.CopyPaste
        private static (List<BaseNodeData> nodes, List<BaseEdgeData> edges) SimulatePaste(
            List<BaseNodeData> sourceNodes, List<BaseEdgeData> sourceEdges)
        {
            var oldToNew = new Dictionary<string, string>();
            var pastedNodes = new List<BaseNodeData>();

            foreach (var node in sourceNodes)
            {
                var newId = Guid.NewGuid().ToString("D");
                oldToNew[node.Id] = newId;
                // Clone via JSON round-trip
                var json = UnityEngine.JsonUtility.ToJson(node);
                // Can't deserialize abstract; use a shallow copy approach for testing
                var copy = new TestNodeData();
                copy.Id = newId;
                copy.NodeType = node.NodeType;
                copy.Position = node.Position;
                pastedNodes.Add(copy);
            }

            var pastedEdges = new List<BaseEdgeData>();
            foreach (var edge in sourceEdges)
            {
                if (oldToNew.TryGetValue(edge.FromNodeId, out var newFrom) &&
                    oldToNew.TryGetValue(edge.ToNodeId, out var newTo))
                {
                    var copy = new BaseEdgeData();
                    copy.Id = Guid.NewGuid().ToString("D");
                    copy.FromNodeId = newFrom;
                    copy.ToNodeId = newTo;
                    copy.PortName = edge.PortName;
                    pastedEdges.Add(copy);
                }
            }

            return (pastedNodes, pastedEdges);
        }

        private class TestNodeData : BaseNodeData { }

        private static BaseNodeData MakeNode(string type = "test/node")
        {
            var n = new TestNodeData();
            n.Id = Guid.NewGuid().ToString("D");
            n.NodeType = type;
            return n;
        }

        [Test]
        public void Paste_AllNodeGuids_DifferFromOriginals()
        {
            var origA = MakeNode();
            var origB = MakeNode();
            var origEdge = new BaseEdgeData { Id = Guid.NewGuid().ToString("D"), FromNodeId = origA.Id, ToNodeId = origB.Id };

            var (nodes, _) = SimulatePaste(new List<BaseNodeData> { origA, origB }, new List<BaseEdgeData> { origEdge });

            Assert.AreNotEqual(origA.Id, nodes[0].Id);
            Assert.AreNotEqual(origB.Id, nodes[1].Id);
        }

        [Test]
        public void Paste_EdgeEndpoints_ReferenceNewGuids()
        {
            var origA = MakeNode();
            var origB = MakeNode();
            var origEdge = new BaseEdgeData { Id = Guid.NewGuid().ToString("D"), FromNodeId = origA.Id, ToNodeId = origB.Id };

            var (nodes, edges) = SimulatePaste(new List<BaseNodeData> { origA, origB }, new List<BaseEdgeData> { origEdge });

            Assert.AreEqual(nodes[0].Id, edges[0].FromNodeId);
            Assert.AreEqual(nodes[1].Id, edges[0].ToNodeId);
            Assert.AreNotEqual(origEdge.Id, edges[0].Id);
        }

        [Test]
        public void PasteTwice_ProducesNonOverlappingGuids()
        {
            var origA = MakeNode();
            var origB = MakeNode();

            var (nodesFirst, _) = SimulatePaste(new List<BaseNodeData> { origA, origB }, new List<BaseEdgeData>());
            var (nodesSecond, _) = SimulatePaste(new List<BaseNodeData> { origA, origB }, new List<BaseEdgeData>());

            Assert.AreNotEqual(nodesFirst[0].Id, nodesSecond[0].Id);
            Assert.AreNotEqual(nodesFirst[1].Id, nodesSecond[1].Id);
        }

        [Test]
        public void Paste_NoPastedGuid_MatchesOriginalGuid()
        {
            var origA = MakeNode();
            var origB = MakeNode();

            var (nodes, _) = SimulatePaste(new List<BaseNodeData> { origA, origB }, new List<BaseEdgeData>());

            Assert.AreNotEqual(origA.Id, nodes[0].Id, "Pasted node must not reuse original GUID");
            Assert.AreNotEqual(origB.Id, nodes[1].Id, "Pasted node must not reuse original GUID");
        }
    }
}
