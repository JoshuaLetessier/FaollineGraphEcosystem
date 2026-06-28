using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Abstract base for all graph node actions. Subclass and implement <see cref="Execute"/>
    /// to define behavior. Attach instances to <see cref="BaseNodeData.OnEnterActions"/>
    /// or <see cref="BaseNodeData.OnExitActions"/>.
    /// </summary>
    [Icon("Assets/FaollineGraphEcosystem/com.faolline.graphcore/Editor/Icons/ico_action.png")]
    public abstract class BaseAction : ScriptableObject
    {
        /// <summary>Executes this action within the given context.</summary>
        public abstract void Execute(BaseContext context);
    }
}
