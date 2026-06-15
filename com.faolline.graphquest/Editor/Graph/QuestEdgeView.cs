using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Concrete edge view for quest graphs. Used as the typed parameter on objective port declarations so Unity's
    /// GraphView instantiates this type when the user draws a prerequisite connection.
    /// </summary>
    public sealed class QuestEdgeView : BaseEdgeView
    {
        public QuestEdgeView() { Initialize(null); }
        public QuestEdgeView(BaseEdgeData data) { Initialize(data); }
    }
}
