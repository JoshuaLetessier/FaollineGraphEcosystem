using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// One-call setup for the "await a scene load, but don't stall forever if it fails" pattern —
    /// <see cref="AsyncSceneLoader"/>/<see cref="AddressablesSceneLoader"/>'s completion + failure signals
    /// otherwise require three separate, easy-to-forget settings on the awaiting node: the completed signal
    /// as <see cref="BaseNodeData.AwaitSignalName"/>, the failed signal appended to
    /// <see cref="BaseNodeData.AwaitSignalNamesExtra"/>, and <see cref="BaseNodeData.ResumeIfSignalAlreadyRaised"/>
    /// so a signal that already fired before this node parked (rare, since both loaders delay a failure
    /// signal by one frame precisely to avoid needing this — but a manually-advanced flow, or an unusually
    /// slow node upstream, can still hit it) resumes the node instead of being missed. This helper sets all
    /// three from the two signals you already configured on the loader.
    /// </summary>
    public static class SceneAwaitSetup
    {
        /// <summary>
        /// Configures <paramref name="node"/> to await either <paramref name="completedSignal"/> or
        /// <paramref name="failedSignal"/> (logical OR — whichever fires first resumes the flow), with
        /// <see cref="BaseNodeData.ResumeIfSignalAlreadyRaised"/> on by default. Call this once per node when
        /// building a graph in code, right after adding a <see cref="LoadSceneAction"/>/
        /// <see cref="UnloadSceneAction"/> to a preceding node's action list.
        /// </summary>
        /// <param name="node">The node that should park until the scene operation lands. Required.</param>
        /// <param name="completedSignal">The loader's <c>LoadCompletedSignal</c>/<c>UnloadCompletedSignal</c>. Required.</param>
        /// <param name="failedSignal">
        /// The loader's <c>LoadFailedSignal</c>/<c>UnloadFailedSignal</c>. Optional — omit only if you have
        /// some other reason to accept the "stalls forever on failure" risk this helper exists to remove.
        /// </param>
        /// <param name="resumeIfAlreadyRaised">
        /// Default <c>true</c>: also resume if either signal was already raised before this node parked (see
        /// the class remarks). Pass <c>false</c> to restore the runner's default live-only behavior.
        /// </param>
        public static void ConfigureLoadAwait(
            BaseNodeData node,
            SignalDef completedSignal,
            SignalDef failedSignal = null,
            bool resumeIfAlreadyRaised = true)
        {
            if (node == null)
            {
                Logging.Error("GraphGameFlow", "[GraphGameFlow] SceneAwaitSetup.ConfigureLoadAwait called with a null node; ignored.");
                return;
            }
            if (completedSignal == null)
            {
                Logging.Error("GraphGameFlow", "[GraphGameFlow] SceneAwaitSetup.ConfigureLoadAwait called with a null completedSignal; ignored.");
                return;
            }

            node.AwaitSignalName = (string)completedSignal;
            if (failedSignal != null) node.AwaitSignalNamesExtra.Add((string)failedSignal);
            node.ResumeIfSignalAlreadyRaised = resumeIfAlreadyRaised;
        }
    }
}
