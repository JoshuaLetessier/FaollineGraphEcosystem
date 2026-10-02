using NUnit.Framework;
using Faolline.GraphLocalization;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>Localized journal text — quest/objective names resolved via deterministic keys.</summary>
    public sealed class QuestLocalizationTests : QuestTestBase
    {
        private const string Csv =
            "Key,en,fr\n" +
            "quest_rescue,Rescue Aldric,Sauver Aldric\n" +
            "objective_find,Find the clue,Trouve l'indice\n" +
            "objective_find_desc,Search the desk.,Fouille le bureau.\n";

        [Test]
        public void GetObjectives_ResolvesKeys_InProviderLocale()
        {
            var provider = new CsvLocalizationProvider(Csv, "fr");
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").Describe("Search the desk.").CompleteWhen(Flag("found"))
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
            var ev = new QuestEvaluator(quest, new QuestContext());
            ev.Evaluate();

            Assert.AreEqual("Rescue Aldric", ev.DisplayName);
            Assert.AreEqual("Find the clue", ev.GetObjectives()[0].DisplayName);
        }

        [Test]
        public void SwitchingLocale_ChangesResolvedText()
        {
            var provider = new CsvLocalizationProvider(Csv, "en");
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").CompleteWhen(Flag("found")).Build());
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
                .AddObjective("raw_id").CompleteWhen(Flag("x")).Build());
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("raw_id", ev.GetObjectives()[0].DisplayName, "no matching key → falls back to authored text (the id)");
        }

        /// <summary>Table-aware backend double recording the table every lookup targeted.</summary>
        private sealed class ScopedProvider : ILocalizationProvider, ITableScopedLocalizationProvider
        {
            public readonly System.Collections.Generic.List<(string table, string key)> ScopedCalls = new();
            public readonly System.Collections.Generic.List<string> ClassicCalls = new();
            public string CurrentLocale => "fr";
            public void SetLocale(string locale) { }
            public string Resolve(string key, string locale) { ClassicCalls.Add(key); return "#" + key; }
            public string ResolveInTable(string table, string key, string locale)
            {
                ScopedCalls.Add((table, key));
                return table == "Q_Test" && key == "quest_rescue" ? "Sauver Aldric" : "#" + key;
            }
        }

        [Test]
        public void JournalTexts_AreLookedUpInTheQuestsOwnTable()
        {
            var provider = new ScopedProvider();
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").Describe("Search the desk.").CompleteWhen(Flag("found"))
                .Build());
            quest.name = "Q_Test";
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("Sauver Aldric", ev.DisplayName);
            var view = ev.GetObjectives()[0];
            Assert.AreEqual("Find the clue", view.DisplayName, "targeted miss → authored text");
            _ = ev.Description;

            Assert.IsNotEmpty(provider.ScopedCalls);
            foreach (var (table, _) in provider.ScopedCalls)
                Assert.AreEqual("Q_Test", table, "every journal text is looked up in the quest's own table");
            Assert.IsEmpty(provider.ClassicCalls, "no untargeted lookup");
        }

        [Test]
        public void TranslationStartingWithHash_IsNotMistakenForAMissingKey()
        {
            // A genuine translation that happens to start with '#' (a hashtag, a room number, "#1 Hunter")
            // must resolve as-is — the missing-key check compares the EXACT "#key" marker for this key, not
            // a bare StartsWith("#").
            var csv = "Key,en\n" +
                      "quest_hash,#1 Hunter\n";
            var provider = new CsvLocalizationProvider(csv, "en");
            var quest = TrackGraph(QuestBuilder.Create("hash").Named("Hunter")
                .AddObjective("obj").CompleteWhen(Flag("x")).Build());
            var ev = new QuestEvaluator(quest, new QuestContext()).UseLocalization(provider);
            ev.Evaluate();

            Assert.AreEqual("#1 Hunter", ev.DisplayName,
                "a translation starting with '#' must not be treated as a missing-key marker");
        }
    }
}
