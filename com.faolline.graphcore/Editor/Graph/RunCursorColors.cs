using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Shared palette for the live-run node highlighting (see <see cref="BaseGraphView"/> run-cursor layer):
    /// one place to tune the border color and thickness per <see cref="GraphRunNodeStatus"/>.
    /// </summary>
    internal static class RunCursorColors
    {
        /// <summary>The two ends the Running border lerps between as it pulses.</summary>
        public static readonly Color RunningBright = new Color(0.35f, 0.70f, 1.00f);
        public static readonly Color RunningDim    = new Color(0.15f, 0.40f, 0.75f);

        /// <summary>The two ends the Waiting border lerps between as it pulses (amber).</summary>
        public static readonly Color WaitingBright = new Color(1.00f, 0.80f, 0.30f);
        public static readonly Color WaitingDim    = new Color(0.65f, 0.48f, 0.12f);

        /// <summary>
        /// The (dim, bright) endpoints a live status pulses between, or <c>false</c> when the status is static.
        /// Both the live cursor (Running) and a parked node (Waiting) pulse; everything else holds.
        /// </summary>
        public static bool TryPulse(GraphRunNodeStatus status, out Color dim, out Color bright)
        {
            switch (status)
            {
                case GraphRunNodeStatus.Running: dim = RunningDim; bright = RunningBright; return true;
                case GraphRunNodeStatus.Waiting: dim = WaitingDim; bright = WaitingBright; return true;
                default: dim = default; bright = default; return false;
            }
        }

        public static Color For(GraphRunNodeStatus status)
        {
            switch (status)
            {
                case GraphRunNodeStatus.Running:   return RunningBright;
                case GraphRunNodeStatus.Active:    return new Color(0.30f, 0.55f, 0.95f);  // solid blue — sub-graph parent
                case GraphRunNodeStatus.Waiting:   return new Color(1.00f, 0.72f, 0.20f);  // amber — parked
                case GraphRunNodeStatus.Visited:   return new Color(0.45f, 0.55f, 0.70f);  // slate — visited trail
                case GraphRunNodeStatus.Ended:     return new Color(0.40f, 0.85f, 0.45f);  // green — ended
                case GraphRunNodeStatus.Available: return new Color(0.30f, 0.80f, 0.80f);  // teal — reactive available
                case GraphRunNodeStatus.Completed: return new Color(0.40f, 0.85f, 0.45f);  // green — completed / fired
                case GraphRunNodeStatus.Locked:    return new Color(0.50f, 0.50f, 0.50f);  // grey — reactive locked
                default:                           return Color.clear;
            }
        }

        public static float WidthFor(GraphRunNodeStatus status)
        {
            switch (status)
            {
                case GraphRunNodeStatus.Visited:
                case GraphRunNodeStatus.Locked: return 2f;   // subtle — context, not focus
                default:                        return 4f;
            }
        }
    }
}
