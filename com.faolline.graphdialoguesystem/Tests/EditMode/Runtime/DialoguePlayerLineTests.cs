using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: line emission + localization through the player.</summary>
    public class DialoguePlayerLineTests
    {
        [Test]
        public void Start_EmitsFirstLine_WithResolvedTextAndSpeaker()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            var speaker = ScriptableObject.CreateInstance<Speaker>();
            speaker.SpeakerId = "npc"; speaker.DisplayNameFallback = "NPC";
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider, _ => speaker);

                LineStep line = null;
                player.OnLine += s => line = s;
                player.Start();

                Assert.IsNotNull(line, "OnLine should fire on Start at a line node.");
                Assert.AreEqual("Hello", line.ResolvedText);
                Assert.AreEqual("NPC", line.ResolvedSpeakerName);
                Assert.AreEqual("npc", line.SpeakerId);
            }
            finally { Object.DestroyImmediate(speaker); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Line_ResolvesInActiveLocale()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "fr");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                LineStep line = null;
                player.OnLine += s => line = s;
                player.Start();

                Assert.AreEqual("Bonjour", line.ResolvedText);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
