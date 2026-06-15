using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Concrete canvas for <see cref="QuestGraph"/> assets: objectives as nodes, prerequisite edges between them
    /// (From→To = "To requires From"). The context menu adds an objective; there are no Start/End nodes (a quest is
    /// a reactive objective DAG, not a runner-walked graph). Not sealed — tests/extensions may subclass it.
    /// </summary>
    public class QuestGraphView : BaseGraphView
    {
        protected override BaseNodeView CreateNodeView(BaseNodeData node)
            => node != null && node.NodeType == ObjectiveNodeData.NodeTypeId
                ? new ObjectiveNodeView((ObjectiveNodeData)node)
                : null;

        protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => new QuestEdgeView(edge);

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            var mousePos = (Vector2)contentViewContainer.transform.matrix.inverse.MultiplyPoint(evt.localMousePosition);
            evt.menu.AppendAction("Add Objective Node", _ =>
                AddNodeToCanvas(new ObjectiveNodeData { NodeType = ObjectiveNodeData.NodeTypeId }, mousePos));
        }
    }
}
