using System;
using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US2 — a cyclic prerequisite topology is rejected at Build() with a diagnostic.</summary>
    public sealed class QuestCycleRejectionTests : QuestTestBase
    {
        [Test]
        public void DirectCycle_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QuestBuilder.Create("q")
                .AddObjective("a").Requires("b").CompleteWhen(Flag("a"))
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b"))
                .Build());
            StringAssert.Contains("[GraphQuest]", ex.Message);
            StringAssert.Contains("cyclic", ex.Message.ToLowerInvariant());
        }

        [Test]
        public void IndirectCycle_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QuestBuilder.Create("q")
                .AddObjective("a").Requires("c").CompleteWhen(Flag("a"))
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b"))
                .AddObjective("c").Requires("b").CompleteWhen(Flag("c"))
                .Build());
            StringAssert.Contains("[GraphQuest]", ex.Message);
        }

        [Test]
        public void UnknownPrerequisite_IsRejected()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => QuestBuilder.Create("q")
                .AddObjective("a").Requires("ghost").CompleteWhen(Flag("a"))
                .Build());
            StringAssert.Contains("[GraphQuest]", ex.Message);
        }
    }
}
