using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// A small draggable dot that lets the user shape a malleable edge: drag to move the bend point,
    /// right-click (or double-click) to remove it. Lives in the graph's content layer (graph space), so it
    /// pans/zooms with the canvas. Created by <see cref="BaseEdgeView"/> while the edge is selected.
    /// </summary>
    internal sealed class WaypointHandle : VisualElement
    {
        private const float Size = 12f;

        private readonly BaseEdgeView _edge;
        private readonly int _index;
        private bool _dragging;

        public WaypointHandle(BaseEdgeView edge, int index)
        {
            _edge = edge;
            _index = index;

            style.position = Position.Absolute;
            style.width = Size;
            style.height = Size;
            style.borderTopLeftRadius = style.borderTopRightRadius =
                style.borderBottomLeftRadius = style.borderBottomRightRadius = Size * 0.5f;
            style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            var border = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = border;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<ContextClickEvent>(OnContextClick);   // right-click removes just this point
        }

        /// <summary>Positions the dot, centered on the given graph-space point.</summary>
        public void SetGraphPosition(Vector2 graphPos)
        {
            style.left = graphPos.x - Size * 0.5f;
            style.top  = graphPos.y - Size * 0.5f;
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;   // only the left button drags; right-click is handled by OnContextClick
            _dragging = true;
            this.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnContextClick(ContextClickEvent e)
        {
            // Remove THIS bend point (not the edge). Stop the event so the edge's own context menu (Delete) does
            // not open. Defer the removal — it rebuilds the handles, which would dispose this one mid-event.
            var edge = _edge;
            int index = _index;
            edge.schedule.Execute(() => edge.RemoveWaypoint(index));
            e.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!_dragging || parent == null) return;
            Vector2 graphPos = parent.WorldToLocal(e.position);   // parent = content layer (graph space)
            SetGraphPosition(graphPos);
            _edge.MoveWaypoint(_index, graphPos);
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_dragging) return;
            _dragging = false;
            this.ReleasePointer(e.pointerId);
            _edge.CommitWaypoints();
            e.StopPropagation();
        }
    }
}
