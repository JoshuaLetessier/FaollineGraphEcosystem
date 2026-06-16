using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>Pure left-to-right layered auto-layout — layering, columns, crossing-reduction, cycle safety.</summary>
    public class GraphAutoLayoutTests
    {
        private static BaseNodeData N(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private static BaseEdgeData E(string from, string to) => new BaseEdgeData { Id = from + to, FromNodeId = from, ToNodeId = to };

        [Test]
        public void Chain_LaysOutLeftToRight()
        {
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("C") };
            var edges = new List<BaseEdgeData> { E("A", "B"), E("B", "C") };

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A");

            Assert.Less(pos["A"].x, pos["B"].x);
            Assert.Less(pos["B"].x, pos["C"].x, "each node sits one column right of its predecessor.");
        }

        [Test]
        public void Sizes_WideNode_DoesNotOverlapNextColumn()
        {
            // A wide source node (e.g. a long dialogue title) must push the next column fully past its right edge.
            var nodes = new List<BaseNodeData> { N("A"), N("B") };
            var edges = new List<BaseEdgeData> { E("A", "B") };
            var sizes = new Dictionary<string, Vector2>
            {
                ["A"] = new Vector2(520f, 90f),   // very wide
                ["B"] = new Vector2(180f, 90f),
            };

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A", nodeSizes: sizes);

            Assert.GreaterOrEqual(pos["B"].x, pos["A"].x + 520f + GraphAutoLayout.ColumnGap - 0.01f,
                "the next column starts past the wide node's right edge + the column gap (no horizontal overlap).");
        }

        [Test]
        public void Sizes_TallNodes_RowStepClearsTheTallest()
        {
            // Two stacked branch nodes that are taller than the default row step must not overlap vertically.
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("C") };
            var edges = new List<BaseEdgeData> { E("A", "B"), E("A", "C") };   // B and C share a column, stacked
            var sizes = new Dictionary<string, Vector2>
            {
                ["A"] = new Vector2(200f, 90f),
                ["B"] = new Vector2(200f, 260f),   // tall
                ["C"] = new Vector2(200f, 260f),   // tall
            };

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A", nodeSizes: sizes);

            Assert.AreEqual(pos["B"].x, pos["C"].x, 0.01f, "B and C still share a column.");
            Assert.GreaterOrEqual(Mathf.Abs(pos["B"].y - pos["C"].y), 260f + GraphAutoLayout.RowGap - 0.01f,
                "stacked nodes are spaced by at least the tallest node's height + the row gap (no vertical overlap).");
        }

        [Test]
        public void Diamond_BranchesShareAColumn_JoinIsRightmost()
        {
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("C"), N("D") };
            var edges = new List<BaseEdgeData> { E("A", "B"), E("A", "C"), E("B", "D"), E("C", "D") };

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A");

            Assert.AreEqual(pos["B"].x, pos["C"].x, 0.01f, "the two branches share a column.");
            Assert.Less(pos["A"].x, pos["B"].x);
            Assert.Less(pos["B"].x, pos["D"].x, "the join is in the rightmost column (after both branches).");
            Assert.AreNotEqual(pos["B"].y, pos["C"].y, "branches in the same column are stacked, not overlapping.");
        }

        [Test]
        public void Cycle_DoesNotHang_AndPlacesAllNodes()
        {
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("C") };
            var edges = new List<BaseEdgeData> { E("A", "B"), E("B", "C"), E("C", "A") };   // loop back to A

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A");

            Assert.AreEqual(3, pos.Count);
            Assert.Less(pos["A"].x, pos["B"].x);
            Assert.Less(pos["B"].x, pos["C"].x, "the loop back-edge is broken; the forward chain still lays out.");
        }

        [Test]
        public void RouteLongEdges_LanesColumnSkippingEdgesBelowTheNodes_Only()
        {
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("C") };
            var edges = new List<BaseEdgeData> { E("A", "B"), E("B", "C"), E("A", "C") };   // A→C skips column B
            var pos = GraphAutoLayout.Arrange(nodes, edges, "A");

            var routes = GraphAutoLayout.RouteLongEdges(pos, edges);

            Assert.IsTrue(routes.ContainsKey("AC"), "the column-skipping edge is routed through a lane.");
            Assert.IsFalse(routes.ContainsKey("AB"), "an adjacent-column edge stays straight.");
            Assert.IsFalse(routes.ContainsKey("BC"));

            float maxNodeY = float.MinValue;
            foreach (var kv in pos) maxNodeY = Mathf.Max(maxNodeY, kv.Value.y);
            foreach (var wp in routes["AC"]) Assert.Greater(wp.y, maxNodeY, "the lane sits below the node rows.");
        }

        [Test]
        public void DisconnectedNode_IsStillPlaced()
        {
            var nodes = new List<BaseNodeData> { N("A"), N("B"), N("Z") };
            var edges = new List<BaseEdgeData> { E("A", "B") };

            var pos = GraphAutoLayout.Arrange(nodes, edges, "A");

            Assert.IsTrue(pos.ContainsKey("Z"), "a node with no edges still gets a position.");
            Assert.AreEqual(3, pos.Count);
        }
    }
}
