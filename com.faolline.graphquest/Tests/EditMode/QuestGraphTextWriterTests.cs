using System.Linq;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;
using Faolline.GraphQuest.Editor;
using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>
    /// 054 write-back for quests: the table text goes back into the quest's Display Name / Description or the
    /// objective's Title / Description — the texts the adapter seeds the tables with, under the same effective
    /// quest id (QuestId, else GraphId).
    /// </summary>
    public sealed class QuestGraphTextWriterTests : QuestTestBase
    {
        private QuestGraph Quest(string questId)
            => TrackGraph(QuestBuilder.Create(questId)
                .Named("Sauver Aldric").Describe("Il est retenu.")
                .AddObjective("find").Named("Trouver l'indice").CompleteWhen(Flag("x"))
                .Build());

        private static ObjectiveNodeData Objective(QuestGraph quest) => quest.Nodes.OfType<ObjectiveNodeData>().Single();

        private static GraphTextDrift Drift(string key, string tableText)
            => new GraphTextDrift("GraphQuest", "g", "Q", "Q", key, LocalizationKeyType.Text, "", "old", tableText);

        private static bool Write(QuestGraph quest, string key, string text)
            => new QuestGraphLocalizationAdapter().WriteGraphText(quest, Drift(key, text));

        [Test]
        public void QuestName_And_Description()
        {
            var quest = Quest("rescue");

            Assert.IsTrue(Write(quest, QuestLocalizationKeys.ForQuest("rescue"), "Libérer Aldric"));
            Assert.IsTrue(Write(quest, QuestLocalizationKeys.ForQuestDescription("rescue"), "Il est captif."));

            Assert.AreEqual("Libérer Aldric", quest.DisplayName);
            Assert.AreEqual("Il est captif.", quest.Description);
        }

        [Test]
        public void QuestWithoutQuestId_UsesTheGraphIdKeys()
        {
            var quest = Quest("");

            Assert.IsTrue(Write(quest, QuestLocalizationKeys.ForQuest(quest.GraphId), "Libérer Aldric"));
            Assert.AreEqual("Libérer Aldric", quest.DisplayName);
        }

        [Test]
        public void ObjectiveTitle_And_Description()
        {
            var quest = Quest("rescue");
            var obj = Objective(quest);

            Assert.IsTrue(Write(quest, QuestLocalizationKeys.ForObjective(obj.Id), "Chercher l'indice"));
            Assert.IsTrue(Write(quest, QuestLocalizationKeys.ForObjectiveDescription(obj.Id), "Près du puits."));

            Assert.AreEqual("Chercher l'indice", obj.Title);
            Assert.AreEqual("Près du puits.", obj.Description);
            Assert.AreEqual("Sauver Aldric", quest.DisplayName, "only the matching text changes");
        }

        [Test]
        public void UnknownKey_WritesNothing()
        {
            var quest = Quest("rescue");

            Assert.IsFalse(Write(quest, "objective_nope", "x"));
            Assert.IsFalse(Write(quest, QuestLocalizationKeys.ForQuest("other"), "x"));
            Assert.AreEqual("Sauver Aldric", quest.DisplayName);
            Assert.AreEqual("Trouver l'indice", Objective(quest).Title);
        }
    }
}
