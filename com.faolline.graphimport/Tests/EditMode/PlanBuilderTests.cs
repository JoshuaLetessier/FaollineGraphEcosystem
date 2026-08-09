using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class PlanBuilderTests
    {
        static List<PivotQuest> MakeQuests() => new List<PivotQuest>
        {
            new PivotQuest("Q_001", "Rencontrer Tsuki",
                new Dictionary<string, string> { ["chapter"] = "Everfrost" },
                new Dictionary<string, IReadOnlyList<PivotReference>>()),
            new PivotQuest("Q_004", "Convaincre le cheval",
                new Dictionary<string, string> { ["chapter"] = "Everfrost" },
                new Dictionary<string, IReadOnlyList<PivotReference>>())
        };

        static PlanBuilder MakeBuilder() => new PlanBuilder(new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
        {
            [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}.asset"
        }));

        [Test]
        public void Build_OneEntryPerQuest_WithProposedPath()
        {
            var plan = MakeBuilder().Build(MakeQuests());

            Assert.AreEqual(2, plan.Entries.Count);
            Assert.AreEqual("Assets/Graphs/Everfrost/Quests/Rencontrer Tsuki.asset", plan.Entries[0].ProposedPath);
            Assert.AreEqual(PlanEntryKind.QuestAsset, plan.Entries[0].Kind);
        }

        [Test]
        public void Build_SameInputTwice_ProducesIdenticalPlan()
        {
            var quests = MakeQuests();
            var builder = MakeBuilder();

            var planA = builder.Build(quests);
            var planB = builder.Build(quests);

            Assert.AreEqual(planA.Entries.Count, planB.Entries.Count);
            for (var i = 0; i < planA.Entries.Count; i++)
            {
                Assert.AreEqual(planA.Entries[i].LogicalId, planB.Entries[i].LogicalId);
                Assert.AreEqual(planA.Entries[i].ProposedPath, planB.Entries[i].ProposedPath);
            }
        }
    }
}
