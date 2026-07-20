using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Deterministic test seam: an <see cref="ISceneLoader"/>/<see cref="ISceneUnloader"/> that records every
    /// requested load/unload instead of touching <c>SceneManager</c>. Lets EditMode tests assert which scenes
    /// a flow would load or unload, with no PlayMode and no real scene activation.
    /// </summary>
    public sealed class StubSceneLoader : ISceneLoader, ISceneUnloader
    {
        /// <summary>Every <see cref="LoadScene"/> call, in order.</summary>
        public readonly List<(string Scene, LoadSceneMode Mode)> Calls = new List<(string, LoadSceneMode)>();

        /// <summary>Every <see cref="UnloadScene"/> call, in order.</summary>
        public readonly List<string> Unloads = new List<string>();

        /// <summary>The scene name of the most recent load, or <c>null</c> if none.</summary>
        public string LastScene => Calls.Count == 0 ? null : Calls[Calls.Count - 1].Scene;

        /// <inheritdoc />
        public void LoadScene(string sceneName, LoadSceneMode mode) => Calls.Add((sceneName, mode));

        /// <inheritdoc />
        public void UnloadScene(string sceneName) => Unloads.Add(sceneName);
    }
}
