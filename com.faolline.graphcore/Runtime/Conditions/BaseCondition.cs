using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Abstract base for all graph conditions. Subclass and implement <see cref="Evaluate"/>
    /// to define logic. Attach instances to <see cref="BaseEdgeData.Condition"/>,
    /// <see cref="BaseChoice.Condition"/>, or <see cref="BaseNodeData.EntryConditions"/>.
    /// </summary>
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_condition.png")]
    public abstract class BaseCondition : ScriptableObject
    {
        /// <summary>Evaluates this condition within the given context.</summary>
        /// <returns><c>true</c> if the condition passes; <c>false</c> otherwise.</returns>
        public abstract bool Evaluate(BaseContext context);
    }
}
