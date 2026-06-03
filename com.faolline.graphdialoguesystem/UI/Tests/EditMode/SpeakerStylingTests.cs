using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>The Canvas view tints the speaker name with the bound speaker's NameColor.</summary>
    public class SpeakerStylingTests
    {
        [Test]
        public void ShowLine_AppliesSpeakerNameColor()
        {
            var go = new GameObject("view");
            var view = go.AddComponent<CanvasDialogueView>();
            var lineGo = new GameObject("line", typeof(RectTransform)); lineGo.transform.SetParent(go.transform);
            var speakerGo = new GameObject("speaker", typeof(RectTransform)); speakerGo.transform.SetParent(go.transform);
            var line = lineGo.AddComponent<TextMeshProUGUI>();
            var speaker = speakerGo.AddComponent<TextMeshProUGUI>();
            view.ConfigureForTest(line, speaker, null, new List<Button>());

            var sp = ScriptableObject.CreateInstance<Speaker>();
            sp.SpeakerId = "npc";
            sp.NameColor = Color.red;
            view.BindSpeakers(new[] { sp });

            try
            {
                view.ShowLine(new LineStep("l", "npc", "NPC", "Hi", "neutral"));
                Assert.AreEqual(Color.red, speaker.color);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sp);
            }
        }
    }
}
