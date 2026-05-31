using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: choice node view dynamic output ports.</summary>
    public class ChoiceNodeViewTests
    {
        [Test]
        public void OneOutputPortPerChoice_PortNameIsChoiceId()
        {
            var data = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            data.Choices.Add(new DialogueChoice { Id = "a", DisplayTextKey = "dlg.yes" });
            data.Choices.Add(new DialogueChoice { Id = "b", DisplayTextKey = "dlg.no" });

            var view = new ChoiceNodeView(data);

            Assert.AreEqual(2, view.OutputPortsLive.Count);
            Assert.AreEqual("a", view.OutputPortsLive[0].portName);
            Assert.AreEqual("b", view.OutputPortsLive[1].portName);
        }

        [Test]
        public void RebuildPorts_ReflectsChoiceListChanges()
        {
            var data = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            data.Choices.Add(new DialogueChoice { Id = "a", DisplayTextKey = "k" });
            var view = new ChoiceNodeView(data);
            Assert.AreEqual(1, view.OutputPortsLive.Count);

            data.Choices.Add(new DialogueChoice { Id = "b", DisplayTextKey = "k2" });
            view.RebuildPorts();
            Assert.AreEqual(2, view.OutputPortsLive.Count);
        }
    }
}
