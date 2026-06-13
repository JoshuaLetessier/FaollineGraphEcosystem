using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Live-run state map: while the game is playing, paints every node of the displayed graph with the status a
    /// registered <see cref="IGraphRunProbe"/> reports — the live cursor (pulsing), the visited trail, sub-graph
    /// parents, and reactive Locked/Available/Completed — the in-game equivalent of the Animator window's running
    /// highlight. Engine-agnostic: it only reads <see cref="GraphRunMonitor"/>, so any host that registers a
    /// probe (BaseRunner, ReactiveEvaluator, FlowRunner) lights this canvas up for free.
    /// </summary>
    public abstract partial class BaseGraphView
    {
        private BaseNodeView _pulseNodeView;            // the live-cursor node currently being pulsed
        private IVisualElementScheduledItem _pulser;

        // Wired from the constructor. Subscribe only while attached to a panel so a closed/reloaded window does
        // not leak a handler on the static monitor, and run the pulse ticker only then.
        private void InitRunCursor()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                GraphRunMonitor.Changed += RefreshRunCursor;
                _pulser = schedule.Execute(TickPulse).Every(33);   // ~30 fps border pulse on the active node
                RefreshRunCursor();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                GraphRunMonitor.Changed -= RefreshRunCursor;
                _pulser?.Pause();
                _pulser = null;
            });
        }

        private void TickPulse()
        {
            if (_pulseNodeView == null) return;
            float k = Mathf.Sin(Time.realtimeSinceStartup * 5f) * 0.5f + 0.5f;
            _pulseNodeView.PulseRunCursor(k);
        }

        /// <summary>
        /// Repaints every node from the probes (status of None clears it), then points the pulse at the live
        /// cursor. No-op visuals outside Play. Cheap (node count is small; fired on user-paced moves).
        /// </summary>
        private void RefreshRunCursor()
        {
            _pulseNodeView = null;

            bool playing = Application.isPlaying && _graph != null;
            var probes = playing ? GraphRunMonitor.Probes : null;

            foreach (var kv in _nodeViews)
            {
                var status = GraphRunNodeStatus.None;
                if (playing)
                {
                    for (int i = 0; i < probes.Count; i++)
                    {
                        var s = probes[i]?.StatusOf(_graph, kv.Key) ?? GraphRunNodeStatus.None;
                        if (s != GraphRunNodeStatus.None) { status = s; break; }   // first probe running this graph wins
                    }
                }

                if (kv.Value.RunStatus != status)
                    kv.Value.SetRunCursor(status);
            }

            if (!playing) return;

            // Focus the pulse on the live cursor (top-of-stack) node, if the active graph is the one shown.
            for (int i = 0; i < probes.Count; i++)
            {
                var id = probes[i]?.ActiveNodeId(_graph);
                if (!string.IsNullOrEmpty(id) && _nodeViews.TryGetValue(id, out var view))
                {
                    _pulseNodeView = view;
                    break;
                }
            }
        }
    }
}
