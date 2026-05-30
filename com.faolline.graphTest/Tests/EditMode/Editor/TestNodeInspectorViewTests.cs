using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        public void BindNode_AfterGraphDestroyed_DoesNotErrorOnSerializedObject()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            var node = new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId };
            graph.AddNode(node);
            _inspector.SetGraph(graph);
            _inspector.BindNode(node);

            Object.DestroyImmediate(graph); // simulate an asset reimport / domain reload

            // Re-selecting the node must not raise "SerializedObject target has been destroyed".
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode must guard against a destroyed SerializedObject target");
        }

        [Test]
        public void SetEndReason_UpdatesNodeReason()
        {
            var node = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId };
            _inspector.BindNode(node);

            _inspector.SetEndReason(node, EndReason.Cancelled);

            Assert.AreEqual(EndReason.Cancelled, node.EndReason,
                "SetEndReason must update the End node's reason");
        }

        [Test]
        public void BindNode_WithSubGraphNodeData_DoesNotThrow()
        {
            var node = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            Assert.DoesNotThrow(() => _inspector.BindNode(node),
                "BindNode with SubGraphNodeData must not throw");
        }

        [Test]
        public void SetSubGraphTarget_AcceptsNonCyclicGraph()
        {
            var graphA = ScriptableObject.CreateInstance<TestGraph>();
            var graphB = ScriptableObject.CreateInstance<TestGraph>();
            var inspector = new TestNodeInspectorView();
            inspector.SetGraph(graphA);
            var node = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            try
            {
                bool accepted = inspector.SetSubGraphTarget(node, graphB);
                Assert.IsTrue(accepted, "A non-cyclic target graph must be accepted");
                Assert.AreSame(graphB, node.TargetGraph);
            }
            finally
            {
                Object.DestroyImmediate(graphA);
                Object.DestroyImmediate(graphB);
            }
        }

        [Test]
        public void SetSubGraphTarget_RefusesSelfReference()
        {
            var graphA = ScriptableObject.CreateInstance<TestGraph>();
            var inspector = new TestNodeInspectorView();
            inspector.SetGraph(graphA);
            var node = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            try
            {
                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(@"Cycle refused"));

                bool accepted = inspector.SetSubGraphTarget(node, graphA);

                Assert.IsFalse(accepted, "Assigning the graph as its own sub-graph must be refused");
                Assert.IsNull(node.TargetGraph, "A refused target must not be stored");
            }
            finally { Object.DestroyImmediate(graphA); }
        }

        [Test]
        public void SetInheritParentContext_UpdatesNode()
        {
            var node = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            _inspector.SetInheritParentContext(node, true);
            Assert.IsTrue(node.InheritParentContext);
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
