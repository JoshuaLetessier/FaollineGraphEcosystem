using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Canvas view for a <see cref="StatementNodeData"/> — the workhorse gameflow node (scene loads via its
    /// enter-actions, signal waits via its await name). One input "in", one output "out". When the node
    /// awaits a signal, a small hint is shown in the body.
    /// </summary>
    public class StatementNodeView : BaseNodeView
    {
        private readonly StatementNodeData _data;

        public StatementNodeView(StatementNodeData data)
        {
            _data = data;
            title = "Statement";
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

            if (_data != null && !string.IsNullOrEmpty(_data.AwaitSignalName))
            {
                var hint = new Label($"await: {_data.AwaitSignalName}");
                hint.AddToClassList("node-label");
                extensionContainer.Add(hint);
                RefreshExpandedState();
            }
        }
    }
}
