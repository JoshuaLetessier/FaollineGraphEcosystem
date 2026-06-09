using UnityEngine.SceneManagement;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// The single seam through which the host bridge loads Unity scenes. The production implementation
    /// (<see cref="UnitySceneLoader"/>) calls <c>SceneManager</c>; tests inject a recording stub so the
    /// driver wiring and the whole scene-flow logic stay verifiable in EditMode, with PlayMode reserved for
    /// the one genuine <c>SceneManager</c> path.
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>Loads <paramref name="sceneName"/> in the given <paramref name="mode"/> (Single/Additive).</summary>
        void LoadScene(string sceneName, LoadSceneMode mode);
    }
}
