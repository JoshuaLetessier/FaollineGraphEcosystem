using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: graph view node-view dispatch by type.</summary>
    public class DialogueGraphViewAddNodeTests
    {
        private DialogueGraphView _gv;

        [SetUp] public void SetUp() => _gv = new DialogueGraphView();

        [Test]
        public void Dispatch_Start() =>
            Assert.IsInstanceOf<StartNodeView>(_gv.CreateNodeViewForTest(
                new StartNodeData { NodeType = StartNodeData.NodeTypeId }));

        [Test]
        public void Dispatch_Line() =>
            Assert.IsInstanceOf<DialogueLineNodeView>(_gv.CreateNodeViewForTest(
                new DialogueLineNodeData { NodeType = DialogueLineNodeData.NodeTypeId }));

        [Test]
        public void Dispatch_Choice() =>
            Assert.IsInstanceOf<ChoiceNodeView>(_gv.CreateNodeViewForTest(
                new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId }));

        [Test]
        public void Dispatch_End() =>
            Assert.IsInstanceOf<EndNodeView>(_gv.CreateNodeViewForTest(
                new EndNodeData { NodeType = EndNodeData.NodeTypeId }));

        [Test]
        public void Dispatch_SubGraph() =>
            Assert.IsInstanceOf<SubGraphNodeView>(_gv.CreateNodeViewForTest(
                new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId }));
    }
}
