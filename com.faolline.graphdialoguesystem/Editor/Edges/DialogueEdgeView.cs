using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Concrete edge view for the dialogue package. Used as the typed parameter for all node port
    /// declarations so Unity's GraphView instantiates this type when the user draws a connection.
    /// </summary>
    public class DialogueEdgeView : BaseEdgeView
    {
        public DialogueEdgeView() { Initialize(null); }

        public DialogueEdgeView(BaseEdgeData data) { Initialize(data); }
    }
}
