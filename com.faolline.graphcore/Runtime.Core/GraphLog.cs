using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Minimal logging seam so Core can warn without depending on <c>UnityEngine.Debug</c>. Both sinks
    /// default to a no-op; the Unity engine layer wires them to <c>Debug.LogWarning</c>/<c>Debug.LogError</c>
    /// once via <c>GraphCoreUnityBootstrap</c> (Runtime assembly). A consumer running Core standalone
    /// (e.g. a plain .NET test project) can assign its own sink instead.
    /// </summary>
    public static class GraphLog
    {
        /// <summary>Receives every warning-level message logged by Core. No-op until assigned.</summary>
        public static Action<string> WarningSink = _ => { };

        /// <summary>Receives every error-level message logged by Core. No-op until assigned.</summary>
        public static Action<string> ErrorSink = _ => { };

        internal static void Warning(string message) => WarningSink?.Invoke(message);
        internal static void Error(string message) => ErrorSink?.Invoke(message);
    }
}
