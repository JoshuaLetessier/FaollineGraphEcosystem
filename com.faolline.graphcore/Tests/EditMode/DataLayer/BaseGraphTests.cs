using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseGraphTests
    {
        private BaseGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_graph);
        }

        // T025: GraphId assigned on OnEnable

        [Test]
        public void BaseGraph_GraphId_IsAssigned_AfterCreation()
        {
            Assert.IsFalse(string.IsNullOrEmpty(_graph.GraphId),
                "GraphId must be non-empty after OnEnable.");
        }

        [Test]
        public void BaseGraph_GraphId_IsValidGuid()
        {
            Assert.IsTrue(System.Guid.TryParse(_graph.GraphId, out _),
                "GraphId must be a valid GUID string.");
        }

        // T026: GraphId not reassigned on second OnEnable

        [Test]
        public void BaseGraph_GraphId_NotReassigned_OnSubsequentOnEnable()
        {
            var firstId = _graph.GraphId;
            typeof(BaseGraph)
                .GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_graph, null);
            Assert.AreEqual(firstId, _graph.GraphId,
                "GraphId must not change after initial assignment.");
        }

        // T027: HistoryDepth defaults to 20

        [Test]
        public void BaseGraph_HistoryDepth_DefaultsTo20()
        {
            Assert.AreEqual(20, _graph.HistoryDepth, "HistoryDepth must default to 20.");
        }

        // T028: lists non-null on creation

        [Test]
        public void BaseGraph_Nodes_IsNonNull_OnCreation()
        {
            Assert.IsNotNull(_graph.Nodes);
        }

        [Test]
        public void BaseGraph_Edges_IsNonNull_OnCreation()
        {
            Assert.IsNotNull(_graph.Edges);
        }

        [Test]
        public void BaseGraph_EntryNodeId_CanBeSetAndRead()
        {
            _graph.EntryNodeId = "start-node-id";
            Assert.AreEqual("start-node-id", _graph.EntryNodeId);
        }

        [Test]
        public void BaseGraph_AddNode_AppearsInNodes()
        {
            var node = new StartNodeData
            {
                Id = System.Guid.NewGuid().ToString("D"),
                NodeType = StartNodeData.NodeTypeId
            };
            _graph.AddNode(node);
            Assert.AreEqual(1, _graph.Nodes.Count);
            Assert.AreSame(node, _graph.Nodes[0]);
        }

        [Test]
        public void BaseGraph_AddEdge_AppearsInEdges()
        {
            var edge = new BaseEdgeData
            {
                Id = System.Guid.NewGuid().ToString("D"),
                FromNodeId = "a",
                ToNodeId = "b"
            };
            _graph.AddEdge(edge);
            Assert.AreEqual(1, _graph.Edges.Count);
        }
    }
}
