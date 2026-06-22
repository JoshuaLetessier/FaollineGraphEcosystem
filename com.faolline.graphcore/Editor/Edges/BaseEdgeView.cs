using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for the visual representation of a <see cref="BaseEdgeData"/> connection.
    /// Override <see cref="HasColorOverride"/> and <see cref="ColorOverride"/> to apply a
    /// custom edge color. Color resolution follows the same three-step chain as
    /// <see cref="BaseNodeView"/>.
    /// </summary>
    public abstract class BaseEdgeView : Edge
    {
        private static readonly string UssName = "BaseEdgeView";

        /// <summary>
        /// Opt-in (off by default): when true, every edge is drawn as a gradient from its SOURCE node's colour to
        /// its TARGET node's colour, so a dense graph stays readable — each end shows which node it links. When
        /// false, the edge keeps its single <see cref="ResolveColor"/> colour. Toggled from the editor toolbar;
        /// the host canvas re-applies colours via <see cref="BaseGraphView"/> when this flips.
        /// </summary>
        public static bool ColorByEndpoints { get; set; }

        /// <summary>The data object this view represents. Null until Initialize is called.</summary>
        public BaseEdgeData EdgeData { get; internal set; }

        /// <summary>
        /// When <c>true</c>, <see cref="ColorOverride"/> is used as the edge color.
        /// Default: <c>false</c>.
        /// </summary>
        protected virtual bool HasColorOverride => false;

        /// <summary>
        /// The color applied when <see cref="HasColorOverride"/> is <c>true</c>.
        /// Default: <c>Color.gray</c>.
        /// </summary>
        protected virtual Color ColorOverride => Color.gray;

        /// <summary>
        /// Resolves the edge color using the three-step chain:
        /// override → lib type color → graphcore default grey.
        /// </summary>
        public Color ResolveColor()
        {
            if (HasColorOverride)
                return ColorOverride;

            if (EdgeData != null && NodeTypeColorRegistry.TryGet(EdgeData.Id, out var registeredColor))
                return registeredColor;

            return GraphCoreDefaults.NodeGrey;
        }

        private OrthogonalEdgeControl _orthoControl;
        private Label _conditionBadge;

        /// <summary>
        /// Uses an <see cref="OrthogonalEdgeControl"/> so the edge renders as a right-angle polyline (through
        /// its data's waypoints) instead of the default bezier that passes under nodes — replacing the control's
        /// render points keeps the native coordinate handling, hit-testing, and connection preview intact.
        /// </summary>
        protected override EdgeControl CreateEdgeControl()
        {
            _orthoControl = new OrthogonalEdgeControl();
            return _orthoControl;
        }

        /// <summary>
        /// Unity's <c>Edge.UpdateEdgeControl</c> resets the control's colours from the PORT colours on every
        /// redraw (hover, move, selection change) — which would wipe the endpoint gradient / a lib's edge colour
        /// the moment the user mouses over a node. So re-assert our resolved colour AFTER the base ran. A selected
        /// edge keeps the native selection highlight; a half-drawn preview (a port still loose) keeps its default.
        /// </summary>
        public override bool UpdateEdgeControl()
        {
            if (!base.UpdateEdgeControl()) return false;
            if (!selected && output != null && input != null)
                ApplyEdgeColor();
            PositionConditionBadge();
            return true;
        }

        /// <summary>
        /// Initializes the view with the given edge data. Call from subclass constructors.
        /// </summary>
        protected void Initialize(BaseEdgeData edgeData)
        {
            EdgeData = edgeData;
            if (_orthoControl != null) _orthoControl.EdgeData = edgeData;
            LoadStyleSheet();
            ApplyEdgeColor();
            InitConditionBadge();
            RegisterCallback<MouseDownEvent>(OnEdgeMouseDown);          // double-click on the line adds a bend point
            RegisterCallback<AttachToPanelEvent>(_ => { RefreshVisual(); RebuildWaypointHandles(); });
            RegisterCallback<DetachFromPanelEvent>(_ => ClearWaypointHandles());
        }

        // ── Condition badge ───────────────────────────────────────────────────────

        private void InitConditionBadge()
        {
            bool hasCondition = EdgeData?.Condition != null;
            if (!hasCondition) return;

            _conditionBadge = new Label("◆")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    fontSize = 12,
                    color = new Color(1f, 0.75f, 0.2f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.9f),
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingLeft = 3,
                    paddingRight = 3,
                    paddingTop = 1,
                    paddingBottom = 1,
                    width = 18,
                    height = 18
                }
            };
            _conditionBadge.tooltip = EdgeData.Condition.GetType().Name;
            Add(_conditionBadge);
        }

        private void PositionConditionBadge()
        {
            if (_conditionBadge == null || edgeControl == null) return;
            var points = edgeControl.controlPoints;
            if (points == null || points.Length < 2) return;

            int midIdx = points.Length / 2;
            var a = points[Mathf.Max(0, midIdx - 1)];
            var b = points[Mathf.Min(points.Length - 1, midIdx)];
            var mid = (a + b) * 0.5f;

            // controlPoints are in the edge's parent space; badge is a child of this edge view,
            // so convert parent → this edge's local space.
            var local = edgeControl.parent != null
                ? edgeControl.parent.ChangeCoordinatesTo(this, mid)
                : mid;

            _conditionBadge.style.left = local.x - 9;
            _conditionBadge.style.top = local.y - 9;
        }

        // ── Malleable waypoints (editor interaction) ──────────────────────────────

        /// <summary>Raised when the edge's waypoints change, so the host graph view can mark itself dirty.</summary>
        public System.Action DataChanged;

        // Handles are tied to the edge's panel lifetime + its waypoints, NOT to selection — clicking a handle
        // (which can deselect the edge) must not tear down the handle mid-drag.
        private readonly List<WaypointHandle> _waypointHandles = new List<WaypointHandle>();

        private GraphView GraphView => GetFirstAncestorOfType<GraphView>();
        private VisualElement ContentLayer => GraphView?.contentViewContainer;

        private void OnEdgeMouseDown(MouseDownEvent e)
        {
            if (e.button != 0 || e.clickCount != 2 || EdgeData == null) return;
            var content = ContentLayer;
            if (content == null) return;

            Vector2 graphPos = content.WorldToLocal(e.mousePosition);
            int index = BestInsertIndex(graphPos);
            EdgeData.Waypoints.Insert(index, graphPos);
            CommitWaypoints();
            RebuildWaypointHandles();
            e.StopPropagation();
        }

        /// <summary>Where a new bend point clicked at <paramref name="graphPos"/> should be inserted, so it
        /// lands on the nearest existing segment rather than always at the end.</summary>
        private int BestInsertIndex(Vector2 graphPos)
        {
            var content = ContentLayer;
            if (content == null || output == null || input == null) return EdgeData.Waypoints.Count;

            var anchors = new List<Vector2> { content.WorldToLocal((Vector2)output.GetGlobalCenter()) };
            anchors.AddRange(EdgeData.Waypoints);
            anchors.Add(content.WorldToLocal((Vector2)input.GetGlobalCenter()));

            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                float d = DistanceToSegment(graphPos, anchors[i], anchors[i + 1]);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;   // segment i sits between anchor[i] and anchor[i+1] ⇒ Waypoints.Insert(i)
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-5f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + t * ab);
        }

        private void RebuildWaypointHandles()
        {
            ClearWaypointHandles();
            var content = ContentLayer;
            if (content == null || EdgeData == null) return;
            for (int i = 0; i < EdgeData.Waypoints.Count; i++)
            {
                var handle = new WaypointHandle(this, i);
                content.Add(handle);
                handle.SetGraphPosition(EdgeData.Waypoints[i]);
                _waypointHandles.Add(handle);
            }
        }

        private void ClearWaypointHandles()
        {
            foreach (var h in _waypointHandles) h.RemoveFromHierarchy();
            _waypointHandles.Clear();
        }

        /// <summary>Live-moves the waypoint at <paramref name="index"/> (called during a handle drag).</summary>
        internal void MoveWaypoint(int index, Vector2 graphPos)
        {
            if (EdgeData == null || index < 0 || index >= EdgeData.Waypoints.Count) return;
            EdgeData.Waypoints[index] = graphPos;
            RefreshVisual();
        }

        /// <summary>Removes the waypoint at <paramref name="index"/> (right-click on its handle).</summary>
        internal void RemoveWaypoint(int index)
        {
            if (EdgeData == null || index < 0 || index >= EdgeData.Waypoints.Count) return;
            EdgeData.Waypoints.RemoveAt(index);
            CommitWaypoints();
            RebuildWaypointHandles();   // indices shifted → rebuild
        }

        /// <summary>Repaints the edge and flags the graph dirty after a waypoint edit settles.</summary>
        internal void CommitWaypoints()
        {
            RefreshVisual();
            DataChanged?.Invoke();
        }

        /// <summary>
        /// Re-routes the edge around the CURRENT node boxes and repaints. The host graph view calls this once the
        /// node geometry has settled (and after a node moves): the obstacle snapshot the router reads is taken at
        /// render time, but the render points are only re-marked dirty on an endpoint move — so an edge laid out
        /// before a sibling node was measured (size still NaN ⇒ ignored as an obstacle), or before that node was
        /// dragged out of the way, would otherwise keep passing UNDER it. Cheap + idempotent.
        /// </summary>
        public void Reroute() => RefreshVisual();

        /// <summary>
        /// Forces the edge to re-route AND repaint live after a waypoint change. The endpoints didn't move, so
        /// the control would otherwise keep its cached render points + the GraphView wouldn't repaint the edge.
        /// </summary>
        private void RefreshVisual()
        {
            _orthoControl?.ForceRerender();
            MarkDirtyRepaint();   // invalidate the edge view itself, not only its control
        }

        private void LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets($"{UssName} t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{UssName}.uss"))
                {
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (styleSheet != null)
                    {
                        styleSheets.Add(styleSheet);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Re-applies the edge colour. Call after the <see cref="ColorByEndpoints"/> toggle flips, or once the
        /// edge has been connected to its ports so the source→target gradient can read its endpoint nodes' colours
        /// (at construction time the ports are not connected yet).
        /// </summary>
        public void RefreshColor() => ApplyEdgeColor();

        private void ApplyEdgeColor()
        {
            // Endpoint-gradient mode: each end takes its connected node's colour (EdgeControl blends across the
            // line), so a dense graph stays readable. Needs the ports connected — falls through to the single
            // colour otherwise (e.g. during construction, or for the live connection preview).
            if (ColorByEndpoints && output?.node is BaseNodeView sourceView && input?.node is BaseNodeView targetView)
            {
                edgeControl.outputColor = sourceView.ResolveColor();   // the 'out' port end = source node
                edgeControl.inputColor  = targetView.ResolveColor();   // the 'in' port end  = target node
                return;
            }

            // Apply resolved edge color. Dynamic registry-based colors require this minimal
            // C# bridge; all layout/typography/spacing styling is in USS.
            edgeControl.inputColor = ResolveColor();
            edgeControl.outputColor = ResolveColor();
        }
    }
}
