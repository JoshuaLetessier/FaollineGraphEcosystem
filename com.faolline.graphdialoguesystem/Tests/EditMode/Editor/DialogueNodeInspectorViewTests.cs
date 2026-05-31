using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: inspector data mutations (choice add/remove, end reason).</summary>
    public class DialogueNodeInspectorViewTests
    {
        private DialogueGraph _graph;
        private DialogueNodeInspectorView _insp;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<DialogueGraph>();
            _insp = new DialogueNodeInspectorView();
            _insp.SetGraph(_graph);
            _insp.SetGraphView(null); // no canvas in this unit test; calls are null-safe
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void AddChoice_AppendsDialogueChoice_WithGuidId()
        {
            var node = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            _graph.AddNode(node);

            _insp.AddChoice(node);

            Assert.AreEqual(1, node.Choices.Count);
            Assert.IsInstanceOf<DialogueChoice>(node.Choices[0]);
            Assert.IsFalse(string.IsNullOrEmpty(node.Choices[0].Id));
        }

        [Test]
        public void RemoveChoice_RemovesIt()
        {
            var node = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            _graph.AddNode(node);
            var choice = new DialogueChoice { Id = "a", DisplayTextKey = "k" };
            node.Choices.Add(choice);

            _insp.RemoveChoice(node, choice);

            Assert.AreEqual(0, node.Choices.Count);
        }

        [Test]
        public void SetEndReason_UpdatesNode()
        {
            var node = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            _graph.AddNode(node);

            _insp.SetEndReason(node, EndReason.Cancelled);

            Assert.AreEqual(EndReason.Cancelled, node.EndReason);
        }
    }
}
