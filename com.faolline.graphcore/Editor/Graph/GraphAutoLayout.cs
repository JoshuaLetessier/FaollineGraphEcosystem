using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Pure auto-layout: assigns each node a clean left-to-right layered position from the graph topology —
    /// longest-path layering on the cycle-broken DAG, then a barycenter pass to reduce edge crossings. No Unity
    /// GraphView dependency, so it is unit-testable headlessly; the editor applies the returned positions.
    /// </summary>
    public static class GraphAutoLayout
    {
        /// <summary>Horizontal gap between layers (columns) when node sizes are unknown.</summary>
        public const float ColumnSpacing = 280f;
        /// <summary>Vertical gap between nodes within a layer.</summary>
        public const float RowSpacing = 150f;

        /// <summary>Gap between a column's widest node and the next column (used when node sizes are provided).</summary>
        public const float ColumnGap = 90f;
        /// <summary>Minimum gap between stacked nodes (added to the tallest node's height when sizes are provided).</summary>
        public const float RowGap = 70f;

        // Fallback box for a node whose measured size is unknown / not yet valid.
        private const float DefaultNodeWidth = 220f;
        private const float DefaultNodeHeight = 90f;

        /// <summary>
        /// Computes a tidy position for every node, keyed by node id. Cycles are handled (back edges are broken
        /// for layering, never followed infinitely); disconnected nodes are still placed.
        /// When <paramref name="nodeSizes"/> is supplied (measured node-view sizes, keyed by id), columns are
        /// spaced by their ACTUAL widths and rows by the tallest node — so wide/tall nodes never overlap. Without
        /// it, uniform <paramref name="columnSpacing"/>/<paramref name="rowSpacing"/> are used (legacy behaviour).
        /// </summary>
        public static Dictionary<string, Vector2> Arrange(
            IReadOnlyList<BaseNodeData> nodes, IReadOnlyList<BaseEdgeData> edges, string entryId,
            float columnSpacing = ColumnSpacing, float rowSpacing = RowSpacing,
            IReadOnlyDictionary<string, Vector2> nodeSizes = null)
        {
            var result = new Dictionary<string, Vector2>();
            if (nodes == null || nodes.Count == 0) return result;

            var ids = new HashSet<string>();
            foreach (var n in nodes) if (n != null && !string.IsNullOrEmpty(n.Id)) ids.Add(n.Id);
            if (ids.Count == 0) return result;

            var succ = new Dictionary<string, List<string>>();
            var pred = new Dictionary<string, List<string>>();
            foreach (var id in ids) { succ[id] = new List<string>(); pred[id] = new List<string>(); }
            if (edges != null)
                foreach (var e in edges)
                {
                    if (e == null || e.FromNodeId == e.ToNodeId) continue;
                    if (!ids.Contains(e.FromNodeId) || !ids.Contains(e.ToNodeId)) continue;
                    succ[e.FromNodeId].Add(e.ToNodeId);
                    pred[e.ToNodeId].Add(e.FromNodeId);
                }

            var backEdges = BackEdges(ids, succ, entryId);

            // Forward DAG (back edges removed) + in-degrees.
            var fsucc = new Dictionary<string, List<string>>();
            var indeg = new Dictionary<string, int>();
            foreach (var id in ids) { fsucc[id] = new List<string>(); indeg[id] = 0; }
            foreach (var id in ids)
                foreach (var v in succ[id])
                    if (!backEdges.Contains((id, v))) { fsucc[id].Add(v); indeg[v]++; }

            // Longest-path layering (Kahn topological order + relaxation).
            var layer = new Dictionary<string, int>();
            foreach (var id in ids) layer[id] = 0;
            var work = new Dictionary<string, int>(indeg);
            var queue = new Queue<string>();
            foreach (var id in ids) if (work[id] == 0) queue.Enqueue(id);
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                foreach (var v in fsucc[u])
                {
                    if (layer[v] < layer[u] + 1) layer[v] = layer[u] + 1;
                    if (--work[v] == 0) queue.Enqueue(v);
                }
            }

            // Group by layer, seeded in the original node order for stability.
            int maxLayer = 0;
            foreach (var id in ids) if (layer[id] > maxLayer) maxLayer = layer[id];
            var layers = new List<List<string>>();
            for (int i = 0; i <= maxLayer; i++) layers.Add(new List<string>());
            foreach (var n in nodes) if (n != null && ids.Contains(n.Id)) layers[layer[n.Id]].Add(n.Id);

            // Barycenter ordering: a few down-sweeps order each layer by its predecessors' average row.
            var row = new Dictionary<string, int>();
            for (int L = 0; L < layers.Count; L++)
                for (int r = 0; r < layers[L].Count; r++) row[layers[L][r]] = r;
            for (int sweep = 0; sweep < 4; sweep++)
                for (int L = 1; L < layers.Count; L++)
                {
                    layers[L].Sort((a, b) => Barycenter(a, pred, row).CompareTo(Barycenter(b, pred, row)));
                    for (int r = 0; r < layers[L].Count; r++) row[layers[L][r]] = r;
                }

            // Column X (left edge) and row step. With measured sizes, columns are spaced by their actual widths
            // (cumulative, so column L+1 starts past column L's widest node + a gap) and the row step grows to the
            // tallest node + a gap — guaranteeing no node ever overlaps another. Without sizes, the legacy uniform
            // grid is kept (existing callers/tests unchanged).
            var colX = new float[layers.Count];
            float rowStep = rowSpacing;
            if (nodeSizes != null)
            {
                float maxH = DefaultNodeHeight;
                foreach (var id in ids) maxH = Mathf.Max(maxH, SizeOf(nodeSizes, id).y);
                rowStep = Mathf.Max(rowSpacing, maxH + RowGap);

                for (int L = 1; L < layers.Count; L++)
                    colX[L] = colX[L - 1] + ColumnWidthAt(layers, nodeSizes, L - 1) + ColumnGap;
            }
            else
            {
                for (int L = 0; L < layers.Count; L++) colX[L] = L * columnSpacing;
            }

            // Positions — each column centered vertically around y = 0.
            for (int L = 0; L < layers.Count; L++)
            {
                var lst = layers[L];
                float offset = (lst.Count - 1) * 0.5f;
                for (int r = 0; r < lst.Count; r++)
                    result[lst[r]] = new Vector2(colX[L], (r - offset) * rowStep);
            }
            return result;
        }

        /// <summary>
        /// Computes bend points for edges that span more than one column, routing each through a lane BELOW the
        /// node rows so it doesn't pass under the intermediate nodes. Keyed by edge id; edges between adjacent
        /// (or same) columns get none (a straight orthogonal link is already clean). Span is detected from the
        /// distinct column positions, so it works whether columns are uniform or width-spaced. Pass the same
        /// <paramref name="nodeSizes"/> as <see cref="Arrange"/> so the lane drops past the source node's actual
        /// right edge. Pair with <see cref="Arrange"/>.
        /// </summary>
        public static Dictionary<string, List<Vector2>> RouteLongEdges(
            Dictionary<string, Vector2> nodePositions, IReadOnlyList<BaseEdgeData> edges,
            float columnSpacing = ColumnSpacing, float nodeHeight = DefaultNodeHeight,
            IReadOnlyDictionary<string, Vector2> nodeSizes = null)
        {
            var result = new Dictionary<string, List<Vector2>>();
            if (nodePositions == null || nodePositions.Count == 0 || edges == null) return result;

            // Distinct column X (left edges), so "spans a column" is width-agnostic.
            var columnXs = new List<float>();
            foreach (var kv in nodePositions)
            {
                bool known = false;
                foreach (var x in columnXs) if (Mathf.Abs(x - kv.Value.x) < 1f) { known = true; break; }
                if (!known) columnXs.Add(kv.Value.x);
            }

            float maxY = float.MinValue, maxBottom = float.MinValue;
            foreach (var kv in nodePositions)
            {
                if (kv.Value.y > maxY) maxY = kv.Value.y;
                float bottom = kv.Value.y + SizeOf(nodeSizes, kv.Key).y;
                if (bottom > maxBottom) maxBottom = bottom;
            }
            float laneBase = Mathf.Max(maxY + nodeHeight, maxBottom) + 70f;

            int laneIndex = 0;
            foreach (var e in edges)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (!nodePositions.TryGetValue(e.FromNodeId, out var from) ||
                    !nodePositions.TryGetValue(e.ToNodeId, out var to)) continue;

                // A column strictly between the endpoints ⇒ the edge would pass under an intermediate node.
                float lo = Mathf.Min(from.x, to.x), hi = Mathf.Max(from.x, to.x);
                int between = 0;
                foreach (var x in columnXs) if (x > lo + 1f && x < hi - 1f) between++;
                if (between < 1) continue;   // adjacent / same column → a straight orthogonal link is already clean

                float laneY = laneBase + laneIndex * 34f;   // stagger lanes so multiple long edges don't overlap
                laneIndex++;
                // Drop into the lane just AFTER the source node (past its out-port, no backtrack) and rise just
                // BEFORE the target node (into its in-port).
                float fromWidth = nodeSizes != null ? SizeOf(nodeSizes, e.FromNodeId).x : columnSpacing * 0.85f;
                float x1 = from.x + fromWidth + 20f;
                float x2 = to.x - 20f;
                result[e.Id] = new List<Vector2> { new Vector2(x1, laneY), new Vector2(x2, laneY) };
            }
            return result;
        }

        // Measured node size, or a sensible default box when unknown / not yet valid.
        private static Vector2 SizeOf(IReadOnlyDictionary<string, Vector2> sizes, string id)
            => sizes != null && id != null && sizes.TryGetValue(id, out var s) && s.x > 1f && s.y > 1f
                ? s : new Vector2(DefaultNodeWidth, DefaultNodeHeight);

        // Widest node in a column (or the default width for an empty column).
        private static float ColumnWidthAt(List<List<string>> layers, IReadOnlyDictionary<string, Vector2> sizes, int column)
        {
            float w = DefaultNodeWidth;
            foreach (var id in layers[column]) w = Mathf.Max(w, SizeOf(sizes, id).x);
            return w;
        }

        // Iterative DFS marking edges to a node currently on the recursion stack as back edges (cycle breakers).
        private static HashSet<(string, string)> BackEdges(
            HashSet<string> ids, Dictionary<string, List<string>> succ, string entryId)
        {
            var back = new HashSet<(string, string)>();
            var color = new Dictionary<string, int>();   // 0 = unvisited, 1 = on stack, 2 = done
            foreach (var id in ids) color[id] = 0;

            var roots = new List<string>();
            if (!string.IsNullOrEmpty(entryId) && ids.Contains(entryId)) roots.Add(entryId);
            foreach (var id in ids) roots.Add(id);   // entry first, then everyone (duplicates skipped by color)

            foreach (var root in roots)
            {
                if (color[root] != 0) continue;
                var stack = new Stack<(string node, int next)>();
                color[root] = 1;
                stack.Push((root, 0));
                while (stack.Count > 0)
                {
                    var (node, next) = stack.Pop();
                    var children = succ[node];
                    if (next < children.Count)
                    {
                        stack.Push((node, next + 1));
                        var child = children[next];
                        if (color[child] == 1) back.Add((node, child));            // edge to an ancestor → back edge
                        else if (color[child] == 0) { color[child] = 1; stack.Push((child, 0)); }
                    }
                    else color[node] = 2;
                }
            }
            return back;
        }

        private static float Barycenter(string node, Dictionary<string, List<string>> pred, Dictionary<string, int> row)
        {
            var ps = pred[node];
            if (ps.Count == 0) return row.TryGetValue(node, out var own) ? own : 0f;
            float sum = 0f; int count = 0;
            foreach (var p in ps) if (row.TryGetValue(p, out var idx)) { sum += idx; count++; }
            return count > 0 ? sum / count : 0f;
        }
    }
}
