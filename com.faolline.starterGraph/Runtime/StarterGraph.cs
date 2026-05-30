using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Concrete graph asset for the StarterGraph verification package.
    /// Serves as the root asset opened by <see cref="Faolline.StarterGraph.Editor.StarterGraphEditorWindow"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Test Graph", fileName = "NewStarterGraph")]
    public class StarterGraph : BaseGraph { }
}
