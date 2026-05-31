using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// EditMode test: two windows on two different dialogue graphs coexist, and loading the second
    /// does not change what the first has loaded (FR-009 — opening a second asset must not disturb the
    /// first). The full focus-or-create asset-double-click path (OnOpenAsset) needs real on-disk assets
    /// and is exercised manually per quickstart; this verifies the underlying per-window isolation.
    /// </summary>
    public class DialogueWindowMultiTests
    {
        [Test]
        public void TwoWindows_HoldIndependentGraphs()
        {
            var w1 = ScriptableObject.CreateInstance<DialogueGraphEditorWindow>();
            var w2 = ScriptableObject.CreateInstance<DialogueGraphEditorWindow>();
            var g1 = ScriptableObject.CreateInstance<DialogueGraph>();
            var g2 = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                w1.LoadGraphForTest(g1);
                w2.LoadGraphForTest(g2);

                Assert.AreSame(g1, w1.LoadedGraphForTest, "Window 1 keeps its own graph.");
                Assert.AreSame(g2, w2.LoadedGraphForTest, "Window 2 loads a different graph...");
                Assert.AreNotSame(w1.LoadedGraphForTest, w2.LoadedGraphForTest,
                    "...without disturbing window 1.");
            }
            finally
            {
                Object.DestroyImmediate(w1);
                Object.DestroyImmediate(w2);
                Object.DestroyImmediate(g1);
                Object.DestroyImmediate(g2);
            }
        }
    }
}
