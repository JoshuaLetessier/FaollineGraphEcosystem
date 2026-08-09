using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class DeclaredColumnBranchStrategyTests
    {
        static PivotQuest MakeQuest() => new PivotQuest("Q_001", "Rencontrer Tsuki",
            new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());

        [Test]
        public void Detect_DistinctOutcomesAtSharedPosition_ProducesOneBranch()
        {
            var quest = MakeQuest();
            var victoire = new PivotStep("S_004", quest, 4, null, "victoire_jd");
            var defaite = new PivotStep("S_005", quest, 4, null, "defaite_jd");
            var intro = new PivotStep("S_001", quest, 0, null, null);

            var (linear, branches) = new DeclaredColumnBranchStrategy().Detect(quest, new[] { intro, victoire, defaite });

            Assert.AreEqual(1, linear.Count);
            Assert.AreEqual("S_001", linear[0].Id);
            Assert.AreEqual(1, branches.Count);
            Assert.AreEqual(4, branches[0].Position);
            CollectionAssert.AreEquivalent(new[] { "S_004", "S_005" }, branches[0].Steps.Select(s => s.Id));
        }

        [Test]
        public void Detect_MissingOutcomeAtSharedPosition_Throws()
        {
            var quest = MakeQuest();
            var a = new PivotStep("S_004", quest, 4, null, "victoire_jd");
            var b = new PivotStep("S_005", quest, 4, null, null); // no declared outcome

            var ex = Assert.Throws<BranchDetectionException>(() => new DeclaredColumnBranchStrategy().Detect(quest, new[] { a, b }));
            Assert.AreEqual(BranchDetectionReason.MissingOutcome, ex.Reason);
        }

        [Test]
        public void Detect_DuplicateOutcomeAtSharedPosition_Throws()
        {
            var quest = MakeQuest();
            var a = new PivotStep("S_004", quest, 4, null, "victoire_jd");
            var b = new PivotStep("S_005", quest, 4, null, "victoire_jd"); // same outcome twice

            var ex = Assert.Throws<BranchDetectionException>(() => new DeclaredColumnBranchStrategy().Detect(quest, new[] { a, b }));
            Assert.AreEqual(BranchDetectionReason.DuplicateOutcome, ex.Reason);
        }

        [Test]
        public void Detect_AllLinearSteps_ProducesNoBranches()
        {
            var quest = MakeQuest();
            var a = new PivotStep("S_001", quest, 0, null, null);
            var b = new PivotStep("S_002", quest, 1, null, null);

            var (linear, branches) = new DeclaredColumnBranchStrategy().Detect(quest, new[] { a, b });

            Assert.AreEqual(2, linear.Count);
            Assert.AreEqual(0, branches.Count);
        }
    }
}
