using NUnit.Framework;
using Faolline.GraphCore.Editor;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class CycleDetectorTests
    {
        private BaseGraph CreateGraph(string graphId = null)
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            if (graphId != null)
            {
                // Force a specific graphId for deterministic testing
                // BaseGraph assigns its own GUID in OnEnable; we rely on that here
            }
            return graph;
        }

        [Test]
        public void Check_NullProposed_ReturnsFalse()
        {
            var root = CreateGraph();
            var result = CycleDetector.Check(root, null);
            Assert.IsFalse(result.HasCycle);
        }

        [Test]
        public void Check_NullRoot_ReturnsFalse()
        {
            var proposed = CreateGraph();
            var result = CycleDetector.Check(null, proposed);
            Assert.IsFalse(result.HasCycle);
        }

        [Test]
        public void Check_SelfCycle_ReturnsTrue()
        {
            var root = CreateGraph();
            var result = CycleDetector.Check(root, root);
            Assert.IsTrue(result.HasCycle);
        }

        [Test]
        public void Check_UnrelatedGraphs_ReturnsFalse()
        {
            var graphA = CreateGraph();
            var graphB = CreateGraph();
            var result = CycleDetector.Check(graphA, graphB);
            Assert.IsFalse(result.HasCycle);
        }

        [Test]
        public void Check_DirectCycle_AReferencesB_CheckBToA_ReturnsTrue()
        {
            var graphA = CreateGraph();
            var graphB = CreateGraph();

            // A already has a SubGraph pointing to B
            var subNode = new SubGraphNodeData();
            subNode.Id = System.Guid.NewGuid().ToString("D");
            subNode.NodeType = SubGraphNodeData.NodeTypeId;
            subNode.TargetGraph = graphB;
            graphA.AddNode(subNode);

            // Proposed: adding B → A would create A → B → A cycle
            var result = CycleDetector.Check(graphB, graphA);
            Assert.IsTrue(result.HasCycle);
            Assert.IsNotEmpty(result.CyclePath);
        }

        [Test]
        public void Check_IndirectCycle_ABC_ReturnsTrue()
        {
            var graphA = CreateGraph();
            var graphB = CreateGraph();
            var graphC = CreateGraph();

            // A → B
            var nodeAB = new SubGraphNodeData();
            nodeAB.Id = System.Guid.NewGuid().ToString("D");
            nodeAB.NodeType = SubGraphNodeData.NodeTypeId;
            nodeAB.TargetGraph = graphB;
            graphA.AddNode(nodeAB);

            // B → C
            var nodeBC = new SubGraphNodeData();
            nodeBC.Id = System.Guid.NewGuid().ToString("D");
            nodeBC.NodeType = SubGraphNodeData.NodeTypeId;
            nodeBC.TargetGraph = graphC;
            graphB.AddNode(nodeBC);

            // Proposed: C → A would create A → B → C → A
            var result = CycleDetector.Check(graphC, graphA);
            Assert.IsTrue(result.HasCycle);
        }

        [Test]
        public void Check_NoSubGraphNodes_ReturnsFalse()
        {
            var graphA = CreateGraph();
            var graphB = CreateGraph();

            // graphB has nodes but none are SubGraphNodeData
            var statementNode = new StatementNodeData();
            statementNode.Id = System.Guid.NewGuid().ToString("D");
            statementNode.NodeType = StatementNodeData.NodeTypeId;
            graphB.AddNode(statementNode);

            var result = CycleDetector.Check(graphA, graphB);
            Assert.IsFalse(result.HasCycle);
        }

        [TearDown]
        public void TearDown()
        {
            // Nothing to clean up; ScriptableObjects created for tests are GC'd
        }
    }
}
