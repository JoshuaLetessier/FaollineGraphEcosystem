using UnityEngine;
using UnityEngine.SceneManagement;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Production <see cref="ISceneLoader"/>: loads a scene through <c>UnityEngine.SceneManagement</c>. A
    /// missing or empty scene name is reported with a <c>[GraphGameFlow]</c> error rather than thrown, so a
    /// misconfigured action never crashes a running flow.
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        /// <inheritdoc />
        public void LoadScene(string sceneName, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GraphGameFlow] UnitySceneLoader.LoadScene called with a null or empty scene name; ignored.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{sceneName}' cannot be loaded (not in Build Settings / Addressables); ignored.");
                return;
            }

            SceneManager.LoadScene(sceneName, mode);
        }
    }
}
