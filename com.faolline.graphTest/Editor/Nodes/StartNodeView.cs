using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Canvas view for <see cref="Faolline.GraphCore.StartNodeData"/>. One output port named "out".
    /// </summary>
    public class StartNodeView : BaseNodeView
    {
        public StartNodeView(StartNodeData data)
        {
            title = "Start";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var output = Port.Create<TestEdgeView>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);
        }
    }
}
