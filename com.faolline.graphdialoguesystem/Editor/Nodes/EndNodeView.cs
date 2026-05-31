using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>Canvas view for <see cref="EndNodeData"/>. One input port "in", no output.</summary>
    public class EndNodeView : BaseNodeView
    {
        private readonly EndNodeData _data;

        public EndNodeView(EndNodeData data)
        {
            _data = data;
            title = "End";
            AddToClassList("gd-node-end");
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);
            RefreshExpandedState();
        }
    }
}
