using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>Canvas view for a <see cref="StartNodeData"/>. One output port "out".</summary>
    public class StartNodeView : BaseNodeView
    {
        public StartNodeView(StartNodeData data)
        {
            title = "Start";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var output = Port.Create<GameFlowEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);
        }
    }
}
