using System.Linq;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class ChoiceNodeViewTests
    {
        private static ChoiceNodeData NewChoiceNode()
            => new ChoiceNodeData { Id = "c1", NodeType = ChoiceNodeData.NodeTypeId };

        [Test]
        public void NewChoiceNode_HasNoOutputPorts()
        {
            var view = new ChoiceNodeView(NewChoiceNode());
            Assert.AreEqual(0, view.OutputPorts.Count,
                "A choice node with no choices must have zero output ports");
        }

        [Test]
        public void HasSingleInputPort()
        {
            var view = new ChoiceNodeView(NewChoiceNode());
            var inputs = view.inputContainer.Children().OfType<Port>().ToList();
            Assert.AreEqual(1, inputs.Count, "Choice node must have exactly one input port");
            Assert.AreEqual("in", inputs[0].portName);
            Assert.AreEqual(Direction.Input, inputs[0].direction);
        }

        [Test]
        public void RebuildPorts_CreatesOnePortPerChoice()
        {
            var node = NewChoiceNode();
            node.Choices.Add(new TestChoice { Id = "id-left",  Label = "Go left" });
            node.Choices.Add(new TestChoice { Id = "id-right", Label = "Go right" });

            var view = new ChoiceNodeView(node);
            view.RebuildPorts();

            Assert.AreEqual(2, view.OutputPorts.Count,
                "RebuildPorts must produce one output port per choice");
        }

        [Test]
        public void OutputPort_PortNameIsChoiceId()
        {
            var node = NewChoiceNode();
            node.Choices.Add(new TestChoice { Id = "id-left",  Label = "Go left" });
            node.Choices.Add(new TestChoice { Id = "id-right", Label = "Go right" });

            var view = new ChoiceNodeView(node);

            var portNames = view.OutputPorts.Select(p => p.portName).ToList();
            CollectionAssert.AreEquivalent(new[] { "id-left", "id-right" }, portNames,
                "Each output port's portName must equal its choice's Id (routing key)");
        }

        [Test]
        public void RebuildPorts_AfterRemovingChoice_DropsPort()
        {
            var node = NewChoiceNode();
            var left  = new TestChoice { Id = "id-left",  Label = "Go left" };
            var right = new TestChoice { Id = "id-right", Label = "Go right" };
            node.Choices.Add(left);
            node.Choices.Add(right);

            var view = new ChoiceNodeView(node);
            node.Choices.Remove(left);
            view.RebuildPorts();

            Assert.AreEqual(1, view.OutputPorts.Count);
            Assert.AreEqual("id-right", view.OutputPorts[0].portName);
        }
    }
}
