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
        /// <summary>
        /// Default port stub length (graph units). The polyline leaves the out-port / enters the in-port along
        /// the port axis for THIS distance before it is free to bend — so the segment touching a port is a clear,
        /// head-on run rather than a corner pressed against the node edge (which reads as "entering from the top").
        /// Generous on purpose; <see cref="Route"/> clamps it to 0.4×edge-length so short edges never overshoot.
        /// </summary>
        public const float DefaultStub = 28f;

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

        /// <summary>
        /// Like <see cref="Route"/>, but routes the polyline AROUND the <paramref name="obstacles"/> (node
        /// rectangles, same coordinate space as the points) instead of straight through them. Each leg between
        /// anchors (port stub → waypoints → port stub) is kept as a simple elbow when that already clears every
        /// obstacle; otherwise it is routed on a grid of the obstacles' (margin-inflated) boundary lines with a
        /// shortest-path search that prefers few turns. The edge's own endpoint nodes must NOT be passed as
        /// obstacles (the line legitimately touches them). Pure — unit-testable; the control calls it per repaint.
        /// </summary>
        public static List<Vector2> RouteAvoiding(Vector2 from, Vector2 to, IReadOnlyList<Vector2> waypoints,
            Vector2 fromDir, Vector2 toDir, IReadOnlyList<Rect> obstacles, float stub = DefaultStub, float margin = 14f)
        {
            if (obstacles == null || obstacles.Count == 0)
                return Route(from, to, waypoints, fromDir, toDir, stub);

            float maxStub = (to - from).magnitude * 0.4f;
            if (maxStub > 0f) stub = Mathf.Min(stub, maxStub);

            var anchors = new List<Vector2> { from, from + fromDir * stub };
            if (waypoints != null) anchors.AddRange(waypoints);
            anchors.Add(to - toDir * stub);
            anchors.Add(to);

            var infl = new List<Rect>(obstacles.Count);
            foreach (var r in obstacles)
                infl.Add(Rect.MinMaxRect(r.xMin - margin, r.yMin - margin, r.xMax + margin, r.yMax + margin));

            var result = new List<Vector2> { anchors[0] };
            for (int i = 1; i < anchors.Count; i++)
            {
                var leg = RouteLeg(result[result.Count - 1], anchors[i], infl);
                for (int j = 1; j < leg.Count; j++) result.Add(leg[j]);
            }
            return Simplify(result);
        }

        // ── Obstacle-avoiding leg routing ───────────────────────────────────────────

        private const float Eps = 0.5f;

        // Routes a single leg a→b orthogonally, avoiding the (already inflated) obstacles that matter for it.
        private static List<Vector2> RouteLeg(Vector2 a, Vector2 b, List<Rect> infl)
        {
            var band = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 1f, Mathf.Min(a.y, b.y) - 1f,
                                       Mathf.Max(a.x, b.x) + 1f, Mathf.Max(a.y, b.y) + 1f);
            List<Rect> obs = null;
            foreach (var r in infl)
                if (r.Overlaps(band)) (obs ??= new List<Rect>()).Add(r);

            if (obs == null) return SimpleL(a, b);

            var clear = SimpleLClear(a, b, obs);
            if (clear != null) return clear;

            return GridRoute(a, b, obs) ?? SimpleL(a, b);
        }

        // The dominant-axis elbow (matches Route's inner logic), no obstacle test.
        private static List<Vector2> SimpleL(Vector2 a, Vector2 b)
        {
            var pts = new List<Vector2> { a };
            if (Approximately(a, b)) return pts;
            bool aligned = Mathf.Approximately(a.x, b.x) || Mathf.Approximately(a.y, b.y);
            if (!aligned)
            {
                var corner = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y)
                    ? new Vector2(b.x, a.y) : new Vector2(a.x, b.y);
                if (!Approximately(corner, a)) pts.Add(corner);
            }
            pts.Add(b);
            return pts;
        }

        // Returns the first simple elbow (or straight line) that clears every obstacle, dominant axis first; else null.
        private static List<Vector2> SimpleLClear(Vector2 a, Vector2 b, List<Rect> obs)
        {
            if (Mathf.Approximately(a.x, b.x) || Mathf.Approximately(a.y, b.y))
                return SegClear(a, b, obs) ? new List<Vector2> { a, b } : null;

            var horizFirst = new Vector2(b.x, a.y);
            var vertFirst  = new Vector2(a.x, b.y);
            var first  = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y) ? horizFirst : vertFirst;
            var second = first == horizFirst ? vertFirst : horizFirst;

            foreach (var corner in new[] { first, second })
                if (SegClear(a, corner, obs) && SegClear(corner, b, obs))
                    return new List<Vector2> { a, corner, b };
            return null;
        }

        // Shortest orthogonal path on the grid of obstacle boundary lines (Dijkstra with a small per-turn penalty).
        private static List<Vector2> GridRoute(Vector2 a, Vector2 b, List<Rect> obs)
        {
            var xs = Lines(a.x, b.x, obs, axisX: true);
            var ys = Lines(a.y, b.y, obs, axisX: false);
            int nx = xs.Count, ny = ys.Count;
            int ia = IndexOf(xs, a.x), ja = IndexOf(ys, a.y);
            int ib = IndexOf(xs, b.x), jb = IndexOf(ys, b.y);
            if (ia < 0 || ja < 0 || ib < 0 || jb < 0) return null;

            const float turn = 16f;
            int States = nx * ny * 2;
            var dist = new float[States];
            var prev = new int[States];
            for (int i = 0; i < States; i++) { dist[i] = float.PositiveInfinity; prev[i] = -1; }

            int St(int xi, int yi, int dir) => (xi * ny + yi) * 2 + dir;

            var pq = new SortedSet<(float cost, int state)>();
            for (int d = 0; d < 2; d++) { int s = St(ia, ja, d); dist[s] = 0f; pq.Add((0f, s)); }

            while (pq.Count > 0)
            {
                var cur = pq.Min; pq.Remove(cur);
                if (cur.cost > dist[cur.state]) continue;
                int xi = cur.state / 2 / ny, yi = cur.state / 2 % ny, dir = cur.state % 2;
                if (xi == ib && yi == jb) break;

                var p = new Vector2(xs[xi], ys[yi]);
                // Horizontal neighbours (dir 0).
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    int n = xi + dx; if (n < 0 || n >= nx) continue;
                    var q = new Vector2(xs[n], ys[yi]);
                    if (SegBlocked(p, q, obs)) continue;
                    Relax(St(n, yi, 0), cur.cost + Mathf.Abs(q.x - p.x) + (dir != 0 ? turn : 0f), cur.state, dist, prev, pq);
                }
                // Vertical neighbours (dir 1).
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    int n = yi + dy; if (n < 0 || n >= ny) continue;
                    var q = new Vector2(xs[xi], ys[n]);
                    if (SegBlocked(p, q, obs)) continue;
                    Relax(St(xi, n, 1), cur.cost + Mathf.Abs(q.y - p.y) + (dir != 1 ? turn : 0f), cur.state, dist, prev, pq);
                }
            }

            int g0 = St(ib, jb, 0), g1 = St(ib, jb, 1);
            int g = dist[g0] <= dist[g1] ? g0 : g1;
            if (float.IsInfinity(dist[g])) return null;

            var path = new List<Vector2>();
            for (int s = g; s != -1; s = prev[s])
                path.Add(new Vector2(xs[s / 2 / ny], ys[s / 2 % ny]));
            path.Reverse();
            return path;
        }

        private static void Relax(int ns, float cost, int from, float[] dist, int[] prev,
            SortedSet<(float, int)> pq)
        {
            if (cost >= dist[ns]) return;
            dist[ns] = cost; prev[ns] = from; pq.Add((cost, ns));
        }

        // Sorted, epsilon-deduped boundary lines on one axis: the two endpoints + each obstacle's two edges.
        private static List<float> Lines(float c0, float c1, List<Rect> obs, bool axisX)
        {
            var raw = new List<float> { c0, c1 };
            foreach (var r in obs)
            {
                raw.Add(axisX ? r.xMin : r.yMin);
                raw.Add(axisX ? r.xMax : r.yMax);
            }
            raw.Sort();
            var outp = new List<float>();
            foreach (var v in raw)
                if (outp.Count == 0 || v - outp[outp.Count - 1] > Eps) outp.Add(v);
            return outp;
        }

        private static int IndexOf(List<float> lines, float v)
        {
            for (int i = 0; i < lines.Count; i++) if (Mathf.Abs(lines[i] - v) <= Eps) return i;
            return -1;
        }

        private static bool SegClear(Vector2 p, Vector2 q, List<Rect> obs) => !SegBlocked(p, q, obs);

        // True when an axis-aligned segment passes strictly through an obstacle's interior (touching an edge is OK).
        private static bool SegBlocked(Vector2 p, Vector2 q, List<Rect> obs)
        {
            foreach (var r in obs)
            {
                if (Mathf.Approximately(p.y, q.y))   // horizontal
                {
                    float y = p.y, x1 = Mathf.Min(p.x, q.x), x2 = Mathf.Max(p.x, q.x);
                    if (y > r.yMin + Eps && y < r.yMax - Eps && x2 > r.xMin + Eps && x1 < r.xMax - Eps) return true;
                }
                else                                  // vertical
                {
                    float x = p.x, y1 = Mathf.Min(p.y, q.y), y2 = Mathf.Max(p.y, q.y);
                    if (x > r.xMin + Eps && x < r.xMax - Eps && y2 > r.yMin + Eps && y1 < r.yMax - Eps) return true;
                }
            }
            return false;
        }

        // Drops consecutive duplicates and collinear midpoints so the path is minimal.
        private static List<Vector2> Simplify(List<Vector2> pts)
        {
            var outp = new List<Vector2>();
            foreach (var p in pts)
            {
                if (outp.Count > 0 && Approximately(outp[outp.Count - 1], p)) continue;
                if (outp.Count >= 2)
                {
                    var a = outp[outp.Count - 2];
                    var b = outp[outp.Count - 1];
                    bool collinear = (Mathf.Approximately(a.x, b.x) && Mathf.Approximately(b.x, p.x)) ||
                                     (Mathf.Approximately(a.y, b.y) && Mathf.Approximately(b.y, p.y));
                    if (collinear) outp[outp.Count - 1] = p;
                    else outp.Add(p);
                }
                else outp.Add(p);
            }
            return outp;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
            => Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }
}
