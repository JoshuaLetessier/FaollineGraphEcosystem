using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>Canvas view for an <see cref="EndNodeData"/>. One input port "in", no outputs.</summary>
    public class EndNodeView : BaseNodeView
    {
        public EndNodeView(EndNodeData data)
        {
            title = "End";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<GameFlowEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);
        }
    }
}
