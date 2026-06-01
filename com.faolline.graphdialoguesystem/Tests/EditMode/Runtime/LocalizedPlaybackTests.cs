using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US4 — the same graph plays in multiple locales with no graph change.</summary>
    public class LocalizedPlaybackTests
    {
        [Test]
        public void LineAndSpeaker_ResolveInActiveLocale_AcrossTwoLanguages()
        {
            var speaker = ScriptableObject.CreateInstance<Speaker>();
            speaker.SpeakerId = "npc"; speaker.DisplayNameFallback = "NPC";
            try
            {
                foreach (var (locale, expectedText, expectedName) in new[]
                {
                    ("en", "Hello", "NPC"),
                    ("fr", "Bonjour", "PNJ"),
                })
                {
                    var graph = DialoguePlayerTestGraphs.Linear();
                    try
                    {
                        var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, locale);
                        var player = new DialoguePlayer(graph, new DialogueContext(), provider, _ => speaker);

                        LineStep line = null;
                        player.OnLine += s => line = s;
                        player.Start();

                        Assert.AreEqual(expectedText, line.ResolvedText, $"text @ {locale}");
                        Assert.AreEqual(expectedName, line.ResolvedSpeakerName, $"speaker @ {locale}");
                    }
                    finally { Object.DestroyImmediate(graph); }
                }
            }
            finally { Object.DestroyImmediate(speaker); }
        }

        [Test]
        public void ChoiceLabels_ResolveInActiveLocale()
        {
            var graph = DialoguePlayerTestGraphs.WithChoice();
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "fr");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                ChoiceStep step = null;
                player.OnChoices += s => step = s;
                player.Start();

                Assert.AreEqual("Oui", step.Options[0].ResolvedLabel);
                Assert.AreEqual("Non", step.Options[1].ResolvedLabel);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
