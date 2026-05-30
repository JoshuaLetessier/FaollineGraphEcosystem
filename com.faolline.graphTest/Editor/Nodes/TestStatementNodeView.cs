using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore.Editor;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Canvas view for <see cref="TestStatementNodeData"/>.
    /// Displays the node's <see cref="TestStatementNodeData.Label"/> in the node body.
    /// One input port "in" and one output port "out".
    /// </summary>
    public class TestStatementNodeView : BaseNodeView
    {
        private readonly TestStatementNodeData _data;

        public TestStatementNodeView(TestStatementNodeData data)
        {
            _data = data;
            title = "Statement";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<TestEdgeView>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            var output = Port.Create<TestEdgeView>(
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
