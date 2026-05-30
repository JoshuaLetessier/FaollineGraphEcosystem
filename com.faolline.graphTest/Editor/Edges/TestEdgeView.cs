using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Concrete edge view for the GraphTest package.
    /// Used as the typed parameter for all node port declarations so Unity's
    /// GraphView instantiates this type when the user draws a connection.
    /// </summary>
    public class TestEdgeView : BaseEdgeView
    {
        public TestEdgeView() { Initialize(null); }

        public TestEdgeView(BaseEdgeData data) { Initialize(data); }
    }
}
