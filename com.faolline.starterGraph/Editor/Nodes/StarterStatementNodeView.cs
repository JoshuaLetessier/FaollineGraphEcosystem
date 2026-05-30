using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore.Editor;
using Faolline.StarterGraph;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Canvas view for <see cref="StarterStatementNodeData"/>.
    /// Displays the node's <see cref="StarterStatementNodeData.Label"/> in the node body.
    /// One input port "in" and one output port "out".
    /// </summary>
    public class StarterStatementNodeView : BaseNodeView
    {
        private readonly StarterStatementNodeData _data;

        public StarterStatementNodeView(StarterStatementNodeData data)
        {
            _data = data;
            title = "Statement";
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

            var label = new Label(string.IsNullOrEmpty(_data?.Label) ? "(no label)" : _data.Label);
            label.AddToClassList("node-label");
            extensionContainer.Add(label);
            RefreshExpandedState();
        }
    }
}
