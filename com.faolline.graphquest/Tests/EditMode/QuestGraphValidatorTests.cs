using System.Linq;
using NUnit.Framework;
using Faolline.GraphCore.Editor;
using Faolline.GraphQuest.Editor;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>
    /// Static quest validation catches authoring mistakes that otherwise fail silently at runtime:
    /// no objectives, an objective that can never auto-complete, an unreachable k-of-N gate, and an
    /// unreachable/misconfigured Threshold rule. #6 dogfood finding.
    /// </summary>
    public sealed class QuestGraphValidatorTests : QuestTestBase
    {
        private static bool HasMessageContaining(GraphValidationReport r, GraphIssueSeverity sev, string fragment)
            => r.Issues.Any(i => i.Severity == sev && i.Message.Contains(fragment));

        [Test]
        public void ValidQuest_HasNoErrors()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").Named("A").CompleteWhen(Flag("a_done"))
                .Build());

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsFalse(report.HasErrors, "a well-formed quest produces no errors");
            Assert.AreEqual(0, report.WarningCount, "and no warnings");
        }

        [Test]
        public void NoObjectives_IsError()
        {
            // The builder rejects an empty quest, but the editor can author one (no nodes) — build it directly.
            var quest = TrackGraph(UnityEngine.ScriptableObject.CreateInstance<QuestGraph>());
            quest.QuestId = "empty";

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(HasMessageContaining(report, GraphIssueSeverity.Error, "no objectives"));
        }

        [Test]
        public void ObjectiveWithoutCompletionCondition_IsWarning()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("stuck").Named("Stuck")   // no CompleteWhen → null completion condition
                .Build());

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsTrue(HasMessageContaining(report, GraphIssueSeverity.Warning, "no Completion Condition"));
        }

        [Test]
        public void UnreachableKofN_IsError()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .AddObjective("join").CompleteWhen(Flag("j")).RequiresAtLeast(3, "a", "b")   // needs 3 of only 2
                .Build());

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsTrue(HasMessageContaining(report, GraphIssueSeverity.Error, "can never unlock"));
        }

        [Test]
        public void ThresholdAboveRequiredCount_IsError()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(5)
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .Build());

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsTrue(HasMessageContaining(report, GraphIssueSeverity.Error, "exceeds"));
        }

        [Test]
        public void NonPositiveThreshold_IsWarning()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(0)
                .AddObjective("a").CompleteWhen(Flag("a"))
                .Build());

            var report = QuestGraphValidator.Validate(quest);

            Assert.IsTrue(HasMessageContaining(report, GraphIssueSeverity.Warning, "≤ 0"));
        }
    }
}
