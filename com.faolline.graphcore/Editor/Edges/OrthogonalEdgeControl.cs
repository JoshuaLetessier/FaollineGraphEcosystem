using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// An <see cref="EdgeControl"/> that draws the edge as a right-angle (orthogonal) polyline through its
    /// data's waypoints instead of the default bezier. It reuses the base control's own endpoint coordinates
    /// (<see cref="EdgeControl.from"/> / <see cref="EdgeControl.to"/>) and merely replaces the tessellated
    /// render points, so coordinate handling, hit-testing, selection, and the connection preview all keep
    /// working. The render-point list has no public accessor, so it is reached by reflection with a graceful
    /// fallback (if Unity renames the internal field, the default bezier is kept — no crash).
    /// </summary>
    public sealed class OrthogonalEdgeControl : EdgeControl
    {
        private static readonly FieldInfo RenderPointsField = FindRenderPointsField();

        private static FieldInfo FindRenderPointsField()
        {
            var t = typeof(EdgeControl);
            var byName = t.GetField("m_RenderPoints", BindingFlags.NonPublic | BindingFlags.Instance);
            if (byName != null && byName.FieldType == typeof(List<Vector2>)) return byName;
            // Fallback: the (single) List<Vector2> instance field on EdgeControl, whatever its name.
            foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                if (f.FieldType == typeof(List<Vector2>)) return f;
            return null;
        }

        private static readonly FieldInfo RenderPointsDirtyField =
            typeof(EdgeControl).GetField("m_RenderPointsDirty", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ControlPointsDirtyField =
            typeof(EdgeControl).GetField("m_ControlPointsDirty", BindingFlags.NonPublic | BindingFlags.Instance);

        // VisualElement.layout has no public setter from this assembly (it is set internally), but EdgeControl
        // assigns it for its manual layout — reach that non-public setter by reflection to extend the pick bbox.
        private static readonly MethodInfo LayoutSetter =
            typeof(VisualElement).GetProperty("layout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetSetMethod(true);

        /// <summary>The edge data carrying optional waypoints (set by the owning view). May be null (preview).</summary>
        public BaseEdgeData EdgeData { get; set; }

        /// <summary>
        /// Recomputes the render points AND the pickable bbox after a waypoint change (the endpoints are
        /// unchanged, so the base would otherwise keep the cached points and the old, too-small bbox — leaving
        /// the bent edge invisible-to-clicks).
        /// </summary>
        public void ForceRerender()
        {
            ControlPointsDirtyField?.SetValue(this, true);   // so UpdateLayout actually recomputes (it guards on this)
            RenderPointsDirtyField?.SetValue(this, true);     // so the next draw re-runs UpdateRenderPoints
            UpdateLayout();                                   // recompute control points + layout + pick bbox now
            MarkDirtyRepaint();
        }

        public override void UpdateLayout()
        {
            base.UpdateLayout();   // refreshes the default layout, but ONLY when the endpoints changed

            // Recompute the pickable bbox FRESH from the endpoints + waypoints (not from the current layout,
            // which would only ever grow). base.UpdateLayout doesn't run on a waypoint edit, so ForceRerender
            // calls this directly to keep a bent edge selectable. ContainsPoint first gates on this bbox.
            if (LayoutSetter == null) return;
            var parentEl = parent;
            if (parentEl == null) return;

            var min = Vector2.Min(from, to);
            var max = Vector2.Max(from, to);
            var wps = EdgeData?.Waypoints;
            if (wps != null && wps.Count > 0)
            {
                var content = GetFirstAncestorOfType<GraphView>()?.contentViewContainer;
                if (content != null)
                    foreach (var wp in wps)
                    {
                        var p = content.ChangeCoordinatesTo(parentEl, wp);
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
            }
            float pad = edgeWidth + 6f;
            LayoutSetter.Invoke(this, new object[]
            {
                new Rect(min.x - pad, min.y - pad, (max.x - min.x) + 2f * pad, (max.y - min.y) + 2f * pad)
            });
        }

        protected override void UpdateRenderPoints()
        {
            base.UpdateRenderPoints();   // computes the default render points + clears the dirty flag

            if (RenderPointsField == null) return;                       // internal field renamed → keep default
            if (!(RenderPointsField.GetValue(this) is List<Vector2> points)) return;
            var parentEl = parent;
            if (parentEl == null) return;

            // EdgeControl.from/to are expressed in the control's PARENT space; route there. Waypoints are stored
            // in graph/content space → convert them into that same parent space.
            List<Vector2> waypointsInParent = null;
            var wps = EdgeData?.Waypoints;
            if (wps != null && wps.Count > 0)
            {
                var content = GetFirstAncestorOfType<GraphView>()?.contentViewContainer;
                if (content != null)
                {
                    waypointsInParent = new List<Vector2>(wps.Count);
                    foreach (var wp in wps)
                        waypointsInParent.Add(content.ChangeCoordinatesTo(parentEl, wp));
                }
            }

            // Leave/enter the ports head-on per their orientation (right for horizontal, down for vertical).
            Vector2 fromDir = outputOrientation == Orientation.Horizontal ? Vector2.right : Vector2.up;
            Vector2 toDir   = inputOrientation  == Orientation.Horizontal ? Vector2.right : Vector2.up;
            var routed = OrthogonalEdgeRouter.Route(from, to, waypointsInParent, fromDir, toDir);

            // Render points live in the EdgeControl's LOCAL space — convert parent → this, exactly as the base
            // does for its control points (EdgeControl.UpdateRenderPoints: parent.ChangeCoordinatesTo(this, cp)).
            points.Clear();
            foreach (var p in routed)
                points.Add(parentEl.ChangeCoordinatesTo(this, p));
        }
    }
}
