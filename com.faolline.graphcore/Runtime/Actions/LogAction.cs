using Faolline.GraphLogging;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: logs a configurable message to the Unity console when executed. Canonical home in
    /// GraphCore; downstream libs subclass this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Log", fileName = "LogAction")]
    public class LogAction : BaseAction
    {
        [SerializeField, Tooltip("Message logged to the Unity console when this action executes.")]
        private string _message;

        [SerializeField, Tooltip("GraphLogging category this message logs under — toggle it off from " +
            "Faolline ▸ Diagnostics ▸ Log Settings to silence this node project-wide without deleting it.")]
        private string _category = "GraphCore.LogAction";

        /// <summary>The message logged to the console when this action executes.</summary>
        public string Message { get => _message; set => _message = value; }

        /// <summary>The GraphLogging category this action's message logs under.</summary>
        public string Category { get => _category; set => _category = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => Logging.Info(_category, $"[GraphCore] {_message}");
    }
}
