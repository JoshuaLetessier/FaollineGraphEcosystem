using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Canvas view for <see cref="ChoiceNodeData"/>'s sibling, <see cref="SubGraphNodeData"/>.
    /// One input port "in" and one output port "out". The target graph and inherit-context flag
    /// are edited in the inspector, not on the node body.
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
            var input = Port.Create<StarterEdgeView>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            var output = Port.Create<StarterEdgeView>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);

            var label = new Label(_data?.TargetGraph != null ? _data.TargetGraph.name : "(no target graph)");
            label.AddToClassList("node-label");
            extensionContainer.Add(label);
            RefreshExpandedState();
        }
    }
}
