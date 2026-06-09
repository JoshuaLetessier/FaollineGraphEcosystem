using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// US1 — state derivation: Locked/Available/Completed derived from topology + completed-set, AND
    /// prerequisites. DAG A,B→C (C requires A and B).
    /// </summary>
    public class ReactiveStateDerivationTests
    {
        private BaseGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            _graph.AddNode(new StatementNodeData { Id = "A", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "B", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddNode(new StatementNodeData { Id = "C", NodeType = StatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "eA", FromNodeId = "A", ToNodeId = "C" });
            _graph.AddEdge(new BaseEdgeData { Id = "eB", FromNodeId = "B", ToNodeId = "C" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void NoPrerequisites_AreAvailable()
        {
            var e = new ReactiveEvaluator(_graph, new BaseContext(), "completed");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("A"));
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("B"));
        }

        [Test]
        public void PartialPrerequisites_LeaveDependentLocked()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "A");
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            Assert.AreEqual(ReactiveNodeState.Completed, e.GetState("A"));
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("B"));
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("C"));
        }

        [Test]
        public void AllPrerequisitesCompleted_DependentAvailable()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "A");
            ctx.AddToCollection("completed", "B");
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            Assert.AreEqual(ReactiveNodeState.Available, e.GetState("C"));
        }

        [Test]
        public void IdInSet_IsCompleted_RegardlessOfPrerequisites()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "C");   // C completed even though A,B are not
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            Assert.AreEqual(ReactiveNodeState.Completed, e.GetState("C"));
        }

        [Test]
        public void UnknownId_IsLocked()
        {
            var e = new ReactiveEvaluator(_graph, new BaseContext(), "completed");
            Assert.AreEqual(ReactiveNodeState.Locked, e.GetState("does-not-exist"));
        }

        [Test]
        public void QuerySets_ReflectDerivation()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "A");
            var e = new ReactiveEvaluator(_graph, ctx, "completed");
            Assert.IsTrue(new List<string>(e.CompletedNodeIds).Contains("A"));
            Assert.IsTrue(new List<string>(e.AvailableNodeIds).Contains("B"));
            Assert.IsFalse(new List<string>(e.AvailableNodeIds).Contains("C"));
        }
    }
}
