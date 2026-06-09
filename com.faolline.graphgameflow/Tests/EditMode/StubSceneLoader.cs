using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Deterministic test seam: an <see cref="ISceneLoader"/> that records every requested load instead of
    /// touching <c>SceneManager</c>. Lets EditMode tests assert which scenes a flow would load, with no
    /// PlayMode and no real scene activation.
    /// </summary>
    public sealed class StubSceneLoader : ISceneLoader
    {
        /// <summary>Every <see cref="LoadScene"/> call, in order.</summary>
        public readonly List<(string Scene, LoadSceneMode Mode)> Calls = new List<(string, LoadSceneMode)>();

        /// <summary>The scene name of the most recent load, or <c>null</c> if none.</summary>
        public string LastScene => Calls.Count == 0 ? null : Calls[Calls.Count - 1].Scene;

        /// <inheritdoc />
        public void LoadScene(string sceneName, LoadSceneMode mode) => Calls.Add((sceneName, mode));
    }
}
