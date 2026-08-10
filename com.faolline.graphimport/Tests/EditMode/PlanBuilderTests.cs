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

        [Test]
        public void BuildDialogues_OneEntryPerDialogue_Deterministic()
        {
            var dialogues = new List<PivotDialogue>
            {
                new PivotDialogue("DLG_006", "Victoire contre le joueur de dé", "n1", new Dictionary<string, PivotDialogueNode>()),
                new PivotDialogue("DLG_008", "Rencontre avec Tsuki", "n1", new Dictionary<string, PivotDialogueNode>())
            };
            var builder = new PlanBuilder(new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/Dialogues/{name}.asset"
            }));

            var planA = builder.BuildDialogues(dialogues);
            var planB = builder.BuildDialogues(dialogues);

            Assert.AreEqual(2, planA.Entries.Count);
            Assert.AreEqual(PlanEntryKind.DialogueAsset, planA.Entries[0].Kind);
            Assert.AreEqual("Assets/Graphs/Dialogues/Victoire contre le joueur de dé.asset", planA.Entries[0].ProposedPath);
            Assert.AreEqual(planA.Entries[0].ProposedPath, planB.Entries[0].ProposedPath);
        }

        [Test]
        public void BuildDialogues_ReferencedDialogueComesBeforeItsReferrer()
        {
            // DLG_006 (listed FIRST) sub-dialogue-links to DLG_008 (listed second) — the plan must
            // still place DLG_008 before DLG_006, since PlanApplier applies entries in list order and
            // a resolver can only find an already-generated asset on disk (see PlanBuilder.OrderByDependency).
            var referrerNodes = new Dictionary<string, PivotDialogueNode>
            {
                ["n1"] = new PivotSubDialogueLink("n1", new PivotReference("Dialogues", "DLG_008"), null)
            };
            var referrer = new PivotDialogue("DLG_006", "Victoire", "n1", referrerNodes);
            var target = new PivotDialogue("DLG_008", "Rencontre", "n1", new Dictionary<string, PivotDialogueNode>());

            var builder = new PlanBuilder(new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/Dialogues/{id}.asset"
            }));

            var plan = builder.BuildDialogues(new List<PivotDialogue> { referrer, target });

            Assert.AreEqual(2, plan.Entries.Count);
            Assert.AreEqual("DLG_008", plan.Entries[0].SourcePivotId, "the referenced dialogue must be applied first");
            Assert.AreEqual("DLG_006", plan.Entries[1].SourcePivotId);
        }
    }
}
