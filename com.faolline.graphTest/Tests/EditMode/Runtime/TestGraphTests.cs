using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestGraphTests
    {
        [Test]
        public void TestGraph_IsBaseGraphSubclass()
        {
            Assert.IsTrue(
                typeof(BaseGraph).IsAssignableFrom(typeof(TestGraph)),
                "TestGraph must be a subclass of BaseGraph");
        }

        [Test]
        public void TestGraph_HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(TestGraph).GetCustomAttributes(typeof(CreateAssetMenuAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "TestGraph must have a [CreateAssetMenu] attribute");
        }

        [Test]
        public void TestGraph_CreateInstance_Succeeds()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            try
            {
                Assert.IsNotNull(graph);
                Assert.IsNotNull(graph.Nodes);
                Assert.IsNotNull(graph.Edges);
            }
            finally { UnityEngine.Object.DestroyImmediate(graph); }
        }

        [Test]
        public void ChoiceNode_WithTwoChoicesAndEdges_SurvivesSerializationRoundTrip()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            TestGraph clone = null;
            try
            {
                var choiceNode = new ChoiceNodeData { Id = "choice", NodeType = ChoiceNodeData.NodeTypeId };
                choiceNode.Choices.Add(new TestChoice { Id = "id-left",  Label = "Go left" });
                choiceNode.Choices.Add(new TestChoice { Id = "id-right", Label = "Go right" });
                var nodeA = new TestStatementNodeData { Id = "a", NodeType = TestStatementNodeData.NodeTypeId };
                var nodeB = new TestStatementNodeData { Id = "b", NodeType = TestStatementNodeData.NodeTypeId };

                graph.AddNode(choiceNode);
                graph.AddNode(nodeA);
                graph.AddNode(nodeB);
                graph.AddEdge(new BaseEdgeData { Id = "eL", FromNodeId = "choice", ToNodeId = "a", PortName = "id-left" });
                graph.AddEdge(new BaseEdgeData { Id = "eR", FromNodeId = "choice", ToNodeId = "b", PortName = "id-right" });

                // Object.Instantiate performs a Unity serialized deep copy — exercises [SerializeReference].
                clone = UnityEngine.Object.Instantiate(graph);

                var clonedChoice = clone.Nodes.OfType<ChoiceNodeData>().FirstOrDefault();
                Assert.IsNotNull(clonedChoice, "Cloned graph must still contain the ChoiceNodeData");
                Assert.AreEqual(2, clonedChoice.Choices.Count, "Both choices must survive serialization");

                var labels = clonedChoice.Choices.OfType<TestChoice>().Select(c => c.Label).ToList();
                CollectionAssert.AreEquivalent(new[] { "Go left", "Go right" }, labels,
                    "Choice labels must be preserved across serialization");

                var ids = clonedChoice.Choices.Select(c => c.Id).ToList();
                CollectionAssert.AreEquivalent(new[] { "id-left", "id-right" }, ids,
                    "Choice Ids must be preserved across serialization");

                Assert.AreEqual(2, clone.Edges.Count, "Both edges must survive serialization");
                var edgePortNames = clone.Edges.Select(e => e.PortName).ToList();
                CollectionAssert.AreEquivalent(new[] { "id-left", "id-right" }, edgePortNames,
                    "Edge PortNames (== choice Ids) must be preserved, keeping routing intact");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }
        }
    }
}
