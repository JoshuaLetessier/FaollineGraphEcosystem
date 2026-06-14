using System;
using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US1 — the builder produces a QuestGraph of ObjectiveNodeData carrying the declared data.</summary>
    public sealed class QuestBuilderFlatTests : QuestTestBase
    {
        [Test]
        public void Build_ProducesObjectiveNodes_WithDeclaredData()
        {
            var done = Flag("x");
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(done).Optional().Build());

            Assert.AreEqual("q", quest.QuestId);
            Assert.AreEqual(1, quest.Nodes.Count);

            var node = quest.Nodes[0] as ObjectiveNodeData;
            Assert.IsNotNull(node, "objective must be an ObjectiveNodeData");
            Assert.AreEqual("a", node.Id);
            Assert.AreEqual(ObjectiveNodeData.NodeTypeId, node.NodeType);
            Assert.AreSame(done, node.CompletionCondition);
            Assert.IsFalse(node.Required, "Optional() ⇒ not required");
        }

        [Test]
        public void Build_EmptyQuest_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QuestBuilder.Create("empty").Build());
            StringAssert.Contains("[GraphQuest]", ex.Message);
        }

        [Test]
        public void Build_DuplicateObjectiveId_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("a").CompleteWhen(Flag("a2"))
                .Build());
            StringAssert.Contains("[GraphQuest]", ex.Message);
        }
    }
}
