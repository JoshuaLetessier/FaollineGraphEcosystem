using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Concrete edge view for the gameflow editor. Used as the typed parameter for every node port so Unity's
    /// GraphView instantiates this type when the user draws a connection.
    /// </summary>
    public class GameFlowEdgeView : BaseEdgeView
    {
        public GameFlowEdgeView() { Initialize(null); }

        public GameFlowEdgeView(BaseEdgeData data) { Initialize(data); }
    }
}
