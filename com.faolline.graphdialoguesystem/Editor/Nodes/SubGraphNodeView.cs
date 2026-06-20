using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Canvas view for <see cref="SubGraphNodeData"/>. One input "in" and one output "out".
    /// Displays the target graph name.
    /// </summary>
    public class SubGraphNodeView : BaseNodeView
    {
        private readonly SubGraphNodeData _data;

        public SubGraphNodeView(SubGraphNodeData data)
        {
            _data = data;
            title = "SubDialogue";
            AddToClassList("gd-node-subgraph");
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            var output = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);

            var label = new Label(_data?.TargetGraph != null ? _data.TargetGraph.name : "(no target)");
            label.AddToClassList("node-label");
            extensionContainer.Add(label);
            RefreshExpandedState();

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount != 2) return;
                GraphEditorWindowRegistry.Open(_data?.TargetGraph);
                evt.StopPropagation();
            });
        }
    }
}
