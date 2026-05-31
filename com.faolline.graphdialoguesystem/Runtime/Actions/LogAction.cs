using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Action that logs a message. Useful for verifying effect execution in tests/demos.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Log Action", fileName = "LogAction")]
    public class LogAction : BaseAction
    {
        [SerializeField] private string _message;
        public string Message { get => _message; set => _message = value; }

        public override void Execute(BaseContext context)
        {
            Debug.Log($"[GraphDialogue] {_message}");
        }
    }
}
