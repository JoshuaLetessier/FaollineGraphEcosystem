using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// The creatable gameflow graph asset — a <see cref="BaseGraph"/> that the gameflow editor window targets
    /// and the <see cref="GraphFlowDriver"/> runs. Create via Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow
    /// Graph. It adds no behavior of its own; the concrete type exists so the editor can target it and the
    /// asset is brandable, exactly as the sibling packages do (StarterGraph, DialogueGraph).
    /// </summary>
    [CreateAssetMenu(menuName = "GraphGameFlow/Game Flow Graph", fileName = "NewGameFlowGraph")]
    public class GameFlowGraph : BaseGraph { }
}
