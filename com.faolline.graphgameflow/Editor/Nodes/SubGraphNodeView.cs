using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Canvas view for a <see cref="SubGraphNodeData"/>. One input "in" and one output "out"; the target
    /// graph and inherit-context flag are edited in the inspector, not on the node body.
    /// </summary>
    public class SubGraphNodeView : BaseNodeView
    {
        private readonly SubGraphNodeData _data;

        public SubGraphNodeView(SubGraphNodeData data)
        {
            _data = data;
            title = "SubGraph";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<GameFlowEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            var output = Port.Create<GameFlowEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);

            var label = new Label(_data?.TargetGraph != null ? _data.TargetGraph.name : "(no target graph)");
            label.AddToClassList("node-label");
            extensionContainer.Add(label);
            RefreshExpandedState();
        }
    }
}
