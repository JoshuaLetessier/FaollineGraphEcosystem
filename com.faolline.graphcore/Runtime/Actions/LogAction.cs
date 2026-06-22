using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: logs a configurable message to the Unity console when executed. Canonical home in
    /// GraphCore; downstream libs subclass this.</summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on node action fields.
    public class LogAction : BaseAction
    {
        [SerializeField] private string _message;

        /// <summary>The message logged to the console when this action executes.</summary>
        public string Message { get => _message; set => _message = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => Debug.Log($"[GraphCore] {_message}");
    }
}
