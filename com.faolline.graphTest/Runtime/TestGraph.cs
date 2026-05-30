using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Concrete graph asset for the GraphTest verification package.
    /// Serves as the root asset opened by <see cref="Faolline.GraphTest.Editor.TestGraphEditorWindow"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Test Graph", fileName = "NewTestGraph")]
    public class TestGraph : BaseGraph { }
}
