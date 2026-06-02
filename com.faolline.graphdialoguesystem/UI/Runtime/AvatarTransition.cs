using System.Collections;
using UnityEngine;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Optional hook to animate avatar changes. Assign a subclass to a <see cref="DialogueViewBase"/> to
    /// run coroutine-based transitions when a speaker/expression swaps (play mode only). When no
    /// transition is assigned, swaps are instantaneous.
    /// </summary>
    public abstract class AvatarTransition : MonoBehaviour
    {
        /// <summary>Animates a freshly-spawned avatar in. Yield until complete.</summary>
        public abstract IEnumerator Spawn(GameObject avatar);

        /// <summary>Animates an avatar out before it is despawned. Yield until complete.</summary>
        public abstract IEnumerator Despawn(GameObject avatar);

        /// <summary>Animates the current avatar moving to the previous-speaker position. Yield until complete.</summary>
        public abstract IEnumerator DemoteToPrevious(GameObject avatar, Transform previousRoot);
    }
}
