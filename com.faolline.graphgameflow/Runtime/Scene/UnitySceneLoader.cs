using UnityEngine;
using UnityEngine.SceneManagement;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Production <see cref="ISceneLoader"/>: loads a scene through <c>UnityEngine.SceneManagement</c>. A
    /// missing or empty scene name is reported with a <c>[GraphGameFlow]</c> error rather than thrown, so a
    /// misconfigured action never crashes a running flow. Also implements <see cref="ISceneUnloader"/>:
    /// unloads go through <c>SceneManager.UnloadSceneAsync</c> (the only non-deprecated unload API — there
    /// is no blocking counterpart), fire-and-forget.
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader, ISceneUnloader
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

        /// <inheritdoc />
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GraphGameFlow] UnitySceneLoader.UnloadScene called with a null or empty scene name; ignored.");
                return;
            }

            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{sceneName}' is not loaded; unload ignored.");
                return;
            }

            if (SceneManager.sceneCount <= 1)
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{sceneName}' is the last loaded scene; Unity cannot unload it. Ignored.");
                return;
            }

            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}
