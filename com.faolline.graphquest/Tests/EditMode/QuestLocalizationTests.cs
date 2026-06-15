using NUnit.Framework;
using Faolline.GraphLocalization;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>Localized journal text — objective/quest names &amp; descriptions resolved as keys via a provider.</summary>
    public sealed class QuestLocalizationTests : QuestTestBase
    {
        private const string Csv =
            "Key,en,fr\n" +
            "quest_rescue,Rescue Aldric,Sauver Aldric\n" +
            "obj_find,Find the clue,Trouve l'indice\n" +
            "obj_find_desc,Search the desk.,Fouille le bureau.\n";

        [Test]
        public void GetObjectives_ResolvesKeys_InProviderLocale()
        {
            var provider = new CsvLocalizationProvider(Csv, "fr");
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("quest_rescue")
                .AddObjective("find").Named("obj_find").Describe("obj_find_desc").CompleteWhen(Flag("found"))
                .Build());
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("Sauver Aldric", ev.DisplayName, "quest title resolved in fr");
            var view = ev.GetObjectives()[0];
            Assert.AreEqual("Trouve l'indice", view.DisplayName, "objective label resolved in fr");
            Assert.AreEqual("Fouille le bureau.", view.Description, "objective description resolved in fr");
        }

        [Test]
        public void WithoutProvider_TextStaysLiteral()
        {
            var quest = TrackGraph(QuestBuilder.Create("rescue").Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").CompleteWhen(Flag("found")).Build());
            var ev = new QuestEvaluator(quest, new QuestContext());   // no provider
            ev.Evaluate();

            Assert.AreEqual("Rescue Aldric", ev.DisplayName);
            Assert.AreEqual("Find the clue", ev.GetObjectives()[0].DisplayName);
        }

        [Test]
        public void SwitchingLocale_ChangesResolvedText()
        {
            var provider = new CsvLocalizationProvider(Csv, "en");
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("find").Named("obj_find").CompleteWhen(Flag("found")).Build());
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("Find the clue", ev.GetObjectives()[0].DisplayName);
            provider.SetLocale("fr");
            Assert.AreEqual("Trouve l'indice", ev.GetObjectives()[0].DisplayName, "re-reads in the new locale");
        }

        [Test]
        public void ObjectiveWithoutTitle_FallsBackToId_NotLocalized()
        {
            var provider = new CsvLocalizationProvider(Csv, "fr");
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("raw_id").CompleteWhen(Flag("x")).Build());   // no .Named ⇒ id fallback
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("raw_id", ev.GetObjectives()[0].DisplayName, "the id fallback is not treated as a key");
        }
    }
}
