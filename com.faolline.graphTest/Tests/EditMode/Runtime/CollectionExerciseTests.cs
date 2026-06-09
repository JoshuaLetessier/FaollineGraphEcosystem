using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// FR-013 — exercises P2 collections end-to-end in the sandbox: a membership-gated edge, a
    /// count-threshold condition, and the recipe (consume-set → produce) action.
    /// </summary>
    [TestFixture]
    public class CollectionExerciseTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _so) Object.DestroyImmediate(o);
            _so.Clear();
        }

        [Test]
        public void MembershipCondition_GatesEdge_InRunner()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            _so.Add(graph);
            var cond = ScriptableObject.CreateInstance<TestCollectionContainsCondition>();
            cond.CollectionKey = "inventory";
            cond.Item = "key";
            _so.Add(cond);

            var start  = new StartNodeData     { Id = "start",  NodeType = StartNodeData.NodeTypeId };
            var open   = new StatementNodeData { Id = "open",   NodeType = StatementNodeData.NodeTypeId };
            var locked = new StatementNodeData { Id = "locked", NodeType = StatementNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.AddNode(open);
            graph.AddNode(locked);
            // Gated edge listed FIRST so SelectEdge prefers it when the key is present.
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "open", Condition = cond });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "start", ToNodeId = "locked" });
            graph.EntryNodeId = "start";

            var ctx = new BaseContext();
            ctx.AddToCollection("inventory", "key");

            var runner = new BaseRunner();
            runner.Start(graph, ctx, new NodeExecutorRegistry());   // at start
            runner.Proceed();                                       // key present → gated edge wins

            Assert.AreEqual("open", runner.CurrentNode.Id);
        }

        [Test]
        public void CountCondition_PassesAtThreshold()
        {
            var cond = ScriptableObject.CreateInstance<TestCollectionCountCondition>();
            cond.CollectionKey = "collected";
            cond.Operator = ComparisonOperator.GreaterOrEqual;
            cond.Value = 3;
            _so.Add(cond);

            var ctx = new BaseContext();
            ctx.AddToCollection("collected", "a");
            ctx.AddToCollection("collected", "b");
            Assert.IsFalse(cond.Evaluate(ctx));

            ctx.AddToCollection("collected", "c");
            Assert.IsTrue(cond.Evaluate(ctx));
        }

        [Test]
        public void Recipe_ConsumesRequired_ProducesReward()
        {
            var recipe = ScriptableObject.CreateInstance<TestRecipeAction>();
            recipe.CollectionKey = "inventory";
            recipe.Required.Add("x");
            recipe.Required.Add("y");
            recipe.Reward = "z";
            _so.Add(recipe);

            var ctx = new BaseContext();
            ctx.AddToCollection("inventory", "x");
            ctx.AddToCollection("inventory", "y");

            recipe.Execute(ctx);

            Assert.IsFalse(ctx.CollectionContains("inventory", "x"));
            Assert.IsFalse(ctx.CollectionContains("inventory", "y"));
            Assert.IsTrue(ctx.CollectionContains("inventory", "z"));
        }

        [Test]
        public void Recipe_MissingIngredient_MakesNoChange()
        {
            var recipe = ScriptableObject.CreateInstance<TestRecipeAction>();
            recipe.CollectionKey = "inventory";
            recipe.Required.Add("x");
            recipe.Required.Add("y");
            recipe.Reward = "z";
            _so.Add(recipe);

            var ctx = new BaseContext();
            ctx.AddToCollection("inventory", "x");   // missing "y"

            recipe.Execute(ctx);

            Assert.IsTrue(ctx.CollectionContains("inventory", "x"), "No change when an ingredient is missing.");
            Assert.IsFalse(ctx.CollectionContains("inventory", "z"));
        }
    }
}
