using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the Speaker type.</summary>
    public class SpeakerTests
    {
        [Test]
        public void TryGetExpression_ReturnsMatch_ThenFallback_ThenFalse()
        {
            var speaker = ScriptableObject.CreateInstance<Speaker>();
            var happyAsset = ScriptableObject.CreateInstance<Speaker>(); // any UnityEngine.Object works as a stand-in
            var fallbackAsset = ScriptableObject.CreateInstance<Speaker>();
            try
            {
                // No expressions, no fallback → false.
                Assert.IsFalse(speaker.TryGetExpression("happy", out _));

                speaker.FallbackExpression = fallbackAsset;
                Assert.IsTrue(speaker.TryGetExpression("unknown", out var fb));
                Assert.AreSame(fallbackAsset, fb);
            }
            finally
            {
                Object.DestroyImmediate(speaker);
                Object.DestroyImmediate(happyAsset);
                Object.DestroyImmediate(fallbackAsset);
            }
        }

        [Test]
        public void DisplayNameFallback_IsCarried()
        {
            var speaker = ScriptableObject.CreateInstance<Speaker>();
            try
            {
                speaker.SpeakerId = "npc_mayor";
                speaker.DisplayNameKey = "speaker.mayor.name";
                speaker.DisplayNameFallback = "Mayor";
                Assert.AreEqual("npc_mayor", speaker.SpeakerId);
                Assert.AreEqual("speaker.mayor.name", speaker.DisplayNameKey);
                Assert.AreEqual("Mayor", speaker.DisplayNameFallback);
            }
            finally { Object.DestroyImmediate(speaker); }
        }
    }
}
