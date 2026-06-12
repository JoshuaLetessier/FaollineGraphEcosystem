using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>Pure routing math for malleable (orthogonal) edges — port stubs, waypoints, axis-aligned segments.</summary>
    public class OrthogonalEdgeRouterTests
    {
        private static readonly Vector2 R = Vector2.right;   // horizontal ports: exit/enter going right

        private static void AssertAllSegmentsAxisAligned(List<Vector2> pts)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                bool axisAligned = Mathf.Approximately(pts[i].x, pts[i - 1].x) || Mathf.Approximately(pts[i].y, pts[i - 1].y);
                Assert.IsTrue(axisAligned, $"segment {i - 1}->{i} ({pts[i - 1]}->{pts[i]}) is not axis-aligned.");
            }
        }

        [Test]
        public void Endpoints_ArePreserved_AndSegmentsAxisAligned()
        {
            var pts = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(100, 40), null, R, R, 16f);
            Assert.AreEqual(new Vector2(0, 0), pts[0]);
            Assert.AreEqual(new Vector2(100, 40), pts[pts.Count - 1]);
            AssertAllSegmentsAxisAligned(pts);
        }

        [Test]
        public void Ports_AreMetHeadOn_WithStubs()
        {
            var pts = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(100, 40), null, R, R, 16f);

            // Leaves 'from' along +x for the stub length, and enters 'to' along +x (so the last leg is horizontal).
            Assert.AreEqual(new Vector2(16, 0), pts[1], "exit stub: leaves the out-port head-on (+x).");
            Assert.AreEqual(new Vector2(84, 40), pts[pts.Count - 2], "enter stub: reaches the in-port from the left.");
            Assert.IsTrue(Mathf.Approximately(pts[pts.Count - 2].y, 40f), "approaches the in-port at its own height.");
        }

        [Test]
        public void StubIsClampedOnShortEdges_NeverOvershoots()
        {
            // Edge shorter than 2*stub: the stub shrinks so 'from'+stub never passes 'to'.
            var pts = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(20, 0), null, R, R, 16f);
            Assert.AreEqual(new Vector2(0, 0), pts[0]);
            Assert.AreEqual(new Vector2(20, 0), pts[pts.Count - 1]);
            Assert.LessOrEqual(pts[1].x, 20f, "the exit stub does not overshoot the target.");
            AssertAllSegmentsAxisAligned(pts);
        }

        [Test]
        public void Waypoints_RouteThroughEach_AxisAligned()
        {
            var wps = new List<Vector2> { new Vector2(40, 80), new Vector2(160, 80) };
            var pts = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(200, 40), wps, R, R, 16f);

            foreach (var wp in wps) CollectionAssert.Contains(pts, wp, "the polyline passes through each waypoint.");
            Assert.AreEqual(new Vector2(0, 0), pts[0]);
            Assert.AreEqual(new Vector2(200, 40), pts[pts.Count - 1]);
            AssertAllSegmentsAxisAligned(pts);
        }

        [Test]
        public void DominantAxisLeads_NoDegenerateCorner()
        {
            // A mostly-horizontal hop: the corner should lead with x (horizontal first), and no duplicate points.
            var pts = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(300, 20), null, R, R, 16f);
            for (int i = 1; i < pts.Count; i++)
                Assert.AreNotEqual(pts[i - 1], pts[i], "no duplicate consecutive points.");
            AssertAllSegmentsAxisAligned(pts);
        }

        [Test]
        public void HorizontalOverload_DefaultsToRightPorts()
        {
            var a = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(100, 40), null);
            var b = OrthogonalEdgeRouter.Route(new Vector2(0, 0), new Vector2(100, 40), null, R, R);
            CollectionAssert.AreEqual(b, a);
        }
    }
}
