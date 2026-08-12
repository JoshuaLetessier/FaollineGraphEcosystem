using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Action that logs a configurable message to the Unity console when executed.
    /// Use to trace node entry or exit events during test graph execution.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Actions/Log Action", fileName = "LogAction")]
    public class TestLogAction : BaseAction
    {
        [SerializeField] private string _message;

        /// <summary>The message logged to the console when this action executes.</summary>
        public string Message { get => _message; set => _message = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            Logging.Info("GraphTest", $"[GraphTest] Action: {_message}");
        }
    }
}
