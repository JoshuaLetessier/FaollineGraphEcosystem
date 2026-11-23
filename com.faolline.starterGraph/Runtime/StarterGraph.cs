using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Concrete graph asset of the StarterGraph template.
    /// Serves as the root asset opened by <see cref="Faolline.StarterGraph.Editor.StarterGraphEditorWindow"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Starter Graph", fileName = "NewStarterGraph")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_graph_starter.png")]
    public class StarterGraph : BaseGraph { }
}
