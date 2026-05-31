using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>Canvas view for <see cref="StartNodeData"/>. One output port "out", no input.</summary>
    public class StartNodeView : BaseNodeView
    {
        private readonly StartNodeData _data;

        public StartNodeView(StartNodeData data)
        {
            _data = data;
            title = "Start";
            AddToClassList("gd-node-start");
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var output = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);
            RefreshExpandedState();
        }
    }
}
