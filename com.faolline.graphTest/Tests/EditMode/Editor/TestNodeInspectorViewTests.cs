using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestNodeInspectorViewTests
    {
        private TestNodeInspectorView _inspector;

        [SetUp]
        public void SetUp()
        {
            _inspector = new TestNodeInspectorView();
        }

        [Test]
        public void BindNode_WithStartNodeData_DoesNotThrow()
        {
            var node = new StartNodeData { Id = "s1", NodeType = StartNodeData.NodeTypeId };
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode with StartNodeData must not throw");
        }

        [Test]
        public void BindNode_WithEndNodeData_DoesNotThrow()
        {
            var node = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId };
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode with EndNodeData must not throw");
        }

        [Test]
        public void BindNode_WithTestStatementNodeData_DoesNotThrow()
        {
            var node = new TestStatementNodeData
            {
                Id = "stmt1",
                NodeType = TestStatementNodeData.NodeTypeId,
                Label = "Hello"
            };
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode with TestStatementNodeData must not throw");
        }

        [Test]
        public void BindNode_WithChoiceNodeData_DoesNotThrow()
        {
            var node = new ChoiceNodeData { Id = "c1", NodeType = ChoiceNodeData.NodeTypeId };
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode with ChoiceNodeData must not throw");
        }

        [Test]
        public void AddChoice_AppendsTestChoiceWithGuidAndLabel()
        {
            var node = new ChoiceNodeData { Id = "c1", NodeType = ChoiceNodeData.NodeTypeId };
            _inspector.BindNode(node);

            _inspector.AddChoice(node);

            Assert.AreEqual(1, node.Choices.Count, "AddChoice must append one choice");
            var choice = node.Choices[0] as TestChoice;
            Assert.IsNotNull(choice, "AddChoice must append a TestChoice");
            Assert.IsFalse(string.IsNullOrEmpty(choice.Id), "Appended choice must have a GUID Id");
            Assert.IsFalse(string.IsNullOrEmpty(choice.Label), "Appended choice must have a default label");
        }

        [Test]
        public void RemoveChoice_DropsChoiceFromNode()
        {
            var node = new ChoiceNodeData { Id = "c1", NodeType = ChoiceNodeData.NodeTypeId };
            _inspector.BindNode(node);
            _inspector.AddChoice(node);
            var choice = node.Choices[0];

            _inspector.RemoveChoice(node, choice);

            Assert.AreEqual(0, node.Choices.Count, "RemoveChoice must drop the choice from the node");
        }

        [Test]
        public void ClearInspector_RemovesAllChildren()
        {
            var node = new TestStatementNodeData
            {
                Id = "stmt1",
                NodeType = TestStatementNodeData.NodeTypeId
            };
            _inspector.BindNode(node);
            _inspector.ClearInspector();

            Assert.AreEqual(0, _inspector.childCount,
                "ClearInspector must remove all child elements");
        }

        [Test]
        public void BindNode_AfterClear_DoesNotAccumulateChildren()
        {
            var node = new TestStatementNodeData
            {
                Id = "stmt1",
                NodeType = TestStatementNodeData.NodeTypeId,
                Label = "First"
            };
            _inspector.BindNode(node);
            int countAfterFirst = _inspector.childCount;

            _inspector.BindNode(node);
            int countAfterSecond = _inspector.childCount;

            Assert.AreEqual(countAfterFirst, countAfterSecond,
                "Calling BindNode twice must not accumulate duplicate children");
        }
    }
}
