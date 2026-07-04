using UnityEditor;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Purges the live-run registries (<see cref="GraphRunMonitor"/>, <see cref="GraphRunContextRegistry"/>)
    /// when Play mode exits. Hosts are expected to detach their own probe when they discard a runner
    /// (<see cref="BaseRunner.DetachEditorProbe"/>), but the static registries would otherwise keep any
    /// forgotten probe alive across sessions — notably with domain reload disabled (Enter Play Mode
    /// Options), where statics survive the Play/Edit boundary and a dead session's probe would shadow the
    /// next session's live cursor.
    /// </summary>
    [InitializeOnLoad]
    internal static class GraphRunMonitorPlayModeReset
    {
        static GraphRunMonitorPlayModeReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode) return;
            GraphRunMonitor.Clear();
            GraphRunContextRegistry.Clear();
        }
    }
}
