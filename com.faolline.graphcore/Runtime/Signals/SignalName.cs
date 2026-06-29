using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named signal as a reusable asset — drag-drop instead of typing a string. Prevents typos,
    /// enables rename-safe references, and is visible in the Project browser. Use the same asset on
    /// a node's <c>AwaitSignalName</c> field and on a <c>ContextTrigger</c>'s signal field to
    /// guarantee they match.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Signal Name", fileName = "NewSignal")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_signal.png")]
    public class SignalName : ScriptableObject
    {
        [SerializeField] private string _name;

        /// <summary>The signal name string. Falls back to the asset name when empty.</summary>
        public string Name => string.IsNullOrEmpty(_name) ? base.name : _name;

        public static implicit operator string(SignalName signal)
            => signal != null ? signal.Name : string.Empty;
    }
}
