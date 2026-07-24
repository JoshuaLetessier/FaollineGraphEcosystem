using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="GraphCategoryGroup"/> is pure editor-time organizational metadata: membership
    /// bookkeeping (dedupe, multi-membership across groups) is the only behaviour it owns.
    /// </summary>
    public class GraphCategoryGroupTests
    {
        [Test]
        public void Label_FallsBackToAssetName_WhenUnset()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            group.name = "Side Quests";
            Assert.AreEqual("Side Quests", group.Label);
            Object.DestroyImmediate(group);
        }

        [Test]
        public void Add_MakesGraphAMember()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            var graph = ScriptableObject.CreateInstance<BaseGraph>();

            group.Add(graph);

            Assert.IsTrue(group.Contains(graph));
            Assert.AreEqual(1, group.Graphs.Count);
            Object.DestroyImmediate(group);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void Add_SameGraphTwice_DoesNotDuplicate()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            var graph = ScriptableObject.CreateInstance<BaseGraph>();

            group.Add(graph);
            group.Add(graph);

            Assert.AreEqual(1, group.Graphs.Count, "adding the same graph twice must not duplicate the entry");
            Object.DestroyImmediate(group);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void Add_Null_IsNoOp()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();

            group.Add(null);

            Assert.AreEqual(0, group.Graphs.Count);
            Object.DestroyImmediate(group);
        }

        [Test]
        public void Remove_DropsMembership()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            group.Add(graph);

            group.Remove(graph);

            Assert.IsFalse(group.Contains(graph));
            Object.DestroyImmediate(group);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void Graph_CanBelongToSeveralGroupsAtOnce()
        {
            var main = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            var chapter1 = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            var graph = ScriptableObject.CreateInstance<BaseGraph>();

            main.Add(graph);
            chapter1.Add(graph);

            Assert.IsTrue(main.Contains(graph));
            Assert.IsTrue(chapter1.Contains(graph), "multi-group membership is intentional, not a gap");
            Object.DestroyImmediate(main);
            Object.DestroyImmediate(chapter1);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void Contains_Null_ReturnsFalse()
        {
            var group = ScriptableObject.CreateInstance<GraphCategoryGroup>();
            Assert.IsFalse(group.Contains(null));
            Object.DestroyImmediate(group);
        }
    }
}
