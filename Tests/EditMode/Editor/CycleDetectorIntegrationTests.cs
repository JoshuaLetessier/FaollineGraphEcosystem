using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Integration tests for CycleDetector in the context of real BaseGraph asset references.
    /// </summary>
    [TestFixture]
    public class CycleDetectorIntegrationTests
    {
        [Test]
        public void Check_GraphAToGraphB_NoReturnPath_NoCycle()
        {
            var graphA = ScriptableObject.CreateInstance<BaseGraph>();
            var graphB = ScriptableObject.CreateInstance<BaseGraph>();

            // A already has a SubGraph node pointing to B
            var subNode = new SubGraphNodeData();
            subNode.Id = System.Guid.NewGuid().ToString("D");
            subNode.NodeType = SubGraphNodeData.NodeTypeId;
            subNode.TargetGraph = graphB;
            graphA.AddNode(subNode);

            // Checking: can we add B → some unrelated graph? No cycle.
            var graphC = ScriptableObject.CreateInstance<BaseGraph>();
            var result = CycleDetector.Check(graphB, graphC);
            Assert.IsFalse(result.HasCycle);
        }

        [Test]
        public void Check_GraphBToGraphA_WhereAAlreadyReferencesB_IsCycle()
        {
            var graphA = ScriptableObject.CreateInstance<BaseGraph>();
            var graphB = ScriptableObject.CreateInstance<BaseGraph>();

            // A → B
            var subNode = new SubGraphNodeData();
            subNode.Id = System.Guid.NewGuid().ToString("D");
            subNode.NodeType = SubGraphNodeData.NodeTypeId;
            subNode.TargetGraph = graphB;
            graphA.AddNode(subNode);

            // Proposed: B → A (would create A → B → A)
            var result = CycleDetector.Check(graphB, graphA);
            Assert.IsTrue(result.HasCycle);
            Assert.IsNotEmpty(result.CyclePath);
        }

        [Test]
        public void Check_CyclePath_ContainsGraphIds()
        {
            var graphA = ScriptableObject.CreateInstance<BaseGraph>();
            var graphB = ScriptableObject.CreateInstance<BaseGraph>();

            var subNode = new SubGraphNodeData();
            subNode.Id = System.Guid.NewGuid().ToString("D");
            subNode.NodeType = SubGraphNodeData.NodeTypeId;
            subNode.TargetGraph = graphB;
            graphA.AddNode(subNode);

            var result = CycleDetector.Check(graphB, graphA);
            Assert.IsTrue(result.HasCycle);
            Assert.Contains(graphA.GraphId, (System.Collections.IList)result.CyclePath);
        }

        [Test]
        public void Check_NonSubGraphEdge_NullProposed_NoCycle()
        {
            var graphA = ScriptableObject.CreateInstance<BaseGraph>();
            // Simulates: edge to a non-SubGraph node → CycleDetector.Check(root, null)
            var result = CycleDetector.Check(graphA, null);
            Assert.IsFalse(result.HasCycle);
        }
    }
}
