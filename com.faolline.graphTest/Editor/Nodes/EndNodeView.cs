using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Canvas view for <see cref="Faolline.GraphCore.EndNodeData"/>. One input port named "in", no outputs.
    /// </summary>
    public class EndNodeView : BaseNodeView
    {
        public EndNodeView(EndNodeData data)
        {
            title = "End";
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
        }
    }
}
