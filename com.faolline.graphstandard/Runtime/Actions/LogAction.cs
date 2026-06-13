using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Domain-neutral action: logs a configurable message to the Unity console when executed.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Actions/Log Action", fileName = "LogAction")]
    public class LogAction : BaseAction
    {
        [SerializeField] private string _message;

        /// <summary>The message logged to the console when this action executes.</summary>
        public string Message { get => _message; set => _message = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => Debug.Log($"[GraphStandard] {_message}");
    }
}
