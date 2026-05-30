using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Concrete edge view for the StarterGraph package.
    /// Used as the typed parameter for all node port declarations so Unity's
    /// GraphView instantiates this type when the user draws a connection.
    /// </summary>
    public class StarterEdgeView : BaseEdgeView
    {
        public StarterEdgeView() { Initialize(null); }

        public StarterEdgeView(BaseEdgeData data) { Initialize(data); }
    }
}
