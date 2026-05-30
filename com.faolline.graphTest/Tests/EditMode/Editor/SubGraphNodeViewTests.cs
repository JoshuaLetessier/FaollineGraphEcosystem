using System.Linq;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class SubGraphNodeViewTests
    {
        private static SubGraphNodeData NewNode()
            => new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };

        [Test]
        public void HasSingleInputPortNamedIn()
        {
            var view = new SubGraphNodeView(NewNode());
            var inputs = view.inputContainer.Children().OfType<Port>().ToList();
            Assert.AreEqual(1, inputs.Count, "SubGraph node must have exactly one input port");
            Assert.AreEqual("in", inputs[0].portName);
            Assert.AreEqual(Direction.Input, inputs[0].direction);
        }

        [Test]
        public void HasSingleOutputPortNamedOut()
        {
            var view = new SubGraphNodeView(NewNode());
            var outputs = view.outputContainer.Children().OfType<Port>().ToList();
            Assert.AreEqual(1, outputs.Count, "SubGraph node must have exactly one output port");
            Assert.AreEqual("out", outputs[0].portName);
            Assert.AreEqual(Direction.Output, outputs[0].direction);
        }
    }
}
