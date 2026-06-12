using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Pure routing math for malleable edges: turns a source point, a target point, optional user bend points,
    /// and the port exit/enter directions into a right-angle (orthogonal) polyline. No Unity GraphView
    /// dependency, so it is unit-testable headlessly; the edge control renders the returned points.
    /// <list type="bullet">
    /// <item><b>Port stubs</b> — the line leaves <paramref name="from"/> along <c>fromDir</c> and enters
    /// <paramref name="to"/> along <c>toDir</c> for a short stub, so it always meets the ports head-on
    /// ("in front of" the connection point) rather than approaching from the side.</item>
    /// <item><b>Dominant-axis corners</b> — between two points the longer leg leads, which reads as a natural
    /// staircase and avoids needless back-tracking loops.</item>
    /// </list>
    /// Every consecutive segment of the result is axis-aligned (same x or same y).
    /// </summary>
    public static class OrthogonalEdgeRouter
    {
        /// <summary>Default port stub length (graph units).</summary>
        public const float DefaultStub = 16f;

        /// <summary>
        /// Routes <paramref name="from"/> → (<paramref name="waypoints"/>) → <paramref name="to"/> as an
        /// orthogonal polyline that leaves/enters the ports along <paramref name="fromDir"/>/<paramref name="toDir"/>.
        /// </summary>
        public static List<Vector2> Route(Vector2 from, Vector2 to, IReadOnlyList<Vector2> waypoints,
            Vector2 fromDir, Vector2 toDir, float stub = DefaultStub)
        {
            // Clamp the stub so it never overshoots on a very short edge.
            float maxStub = (to - from).magnitude * 0.4f;
            if (maxStub > 0f) stub = Mathf.Min(stub, maxStub);

            var anchors = new List<Vector2> { from, from + fromDir * stub };
            if (waypoints != null) anchors.AddRange(waypoints);
            anchors.Add(to - toDir * stub);
            anchors.Add(to);

            var pts = new List<Vector2> { anchors[0] };
            for (int i = 1; i < anchors.Count; i++)
            {
                var p = pts[pts.Count - 1];
                var q = anchors[i];
                if (Approximately(p, q)) continue;

                bool aligned = Mathf.Approximately(p.x, q.x) || Mathf.Approximately(p.y, q.y);
                if (!aligned)
                {
                    // Lead with the dominant axis (the longer leg) → natural elbow, no back-tracking.
                    var corner = Mathf.Abs(q.x - p.x) >= Mathf.Abs(q.y - p.y)
                        ? new Vector2(q.x, p.y)   // horizontal first
                        : new Vector2(p.x, q.y);  // vertical first
                    if (!Approximately(corner, p)) pts.Add(corner);
                }
                pts.Add(q);
            }
            return pts;
        }

        /// <summary>Convenience overload assuming horizontal ports (out exits right, in entered from the left).</summary>
        public static List<Vector2> Route(Vector2 from, Vector2 to, IReadOnlyList<Vector2> waypoints)
            => Route(from, to, waypoints, Vector2.right, Vector2.right);

        private static bool Approximately(Vector2 a, Vector2 b)
            => Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }
}
