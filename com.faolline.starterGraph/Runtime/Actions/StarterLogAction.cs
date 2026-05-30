using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Action that logs a configurable message to the Unity console when executed.
    /// Use to trace node entry or exit events during test graph execution.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Actions/Log Action", fileName = "LogAction")]
    public class StarterLogAction : BaseAction
    {
        [SerializeField] private string _message;

        /// <summary>The message logged to the console when this action executes.</summary>
        public string Message { get => _message; set => _message = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            Debug.Log($"[StarterGraph] Action: {_message}");
        }
    }
}
