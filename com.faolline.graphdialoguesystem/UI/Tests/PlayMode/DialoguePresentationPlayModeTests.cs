using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>PlayMode coverage for time-based presentation: typewriter reveal/skip and driver auto-advance.</summary>
    public class DialoguePresentationPlayModeTests
    {
        private sealed class FakeView : IDialogueView
        {
            public LineStep LastLine;
            public event Action<string> ChoiceSelected;
            public void BindSpeakers(IReadOnlyList<Speaker> s) { }
            public void ShowLine(LineStep step) => LastLine = step;
            public void ShowChoices(ChoiceStep step) { }
            public void HideAll() { }
            public void Raise(string id) => ChoiceSelected?.Invoke(id);
        }

        private static CanvasDialogueView NewCanvasView(out TMP_Text line, out GameObject go)
        {
            go = new GameObject("view");
            var view = go.AddComponent<CanvasDialogueView>();
            var lineGo = new GameObject("line", typeof(RectTransform));
            lineGo.transform.SetParent(go.transform);
            line = lineGo.AddComponent<TextMeshProUGUI>();
            view.ConfigureForTest(line, null, null, new List<Button>());
            return view;
        }

        [UnityTest]
        public IEnumerator Typewriter_RevealsProgressively_ThenCompletes()
        {
            var view = NewCanvasView(out var line, out var go);
            view.ConfigureTypewriterForTest(true, 10f); // 10 chars/sec
            view.ShowLine(new LineStep("l", null, "", "Hello world", "neutral"));

            Assert.IsTrue(view.IsTyping, "Should be typing right after ShowLine.");
            Assert.AreNotEqual("Hello world", line.text, "Text should not be complete yet.");

            yield return new WaitForSeconds(2f); // 11 chars at 10/s ≈ 1.1s

            Assert.IsFalse(view.IsTyping);
            Assert.AreEqual("Hello world", line.text);
            UnityEngine.Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator SkipTyping_CompletesImmediately()
        {
            var view = NewCanvasView(out var line, out var go);
            view.ConfigureTypewriterForTest(true, 3f);
            view.ShowLine(new LineStep("l", null, "", "Hello world", "neutral"));

            Assert.IsTrue(view.IsTyping);
            view.SkipTyping();

            Assert.IsFalse(view.IsTyping);
            Assert.AreEqual("Hello world", line.text);
            yield return null;
            UnityEngine.Object.Destroy(go);
        }

        // Start → l1 → l2 → End
        private static DialogueGraph BuildTwoLineGraph()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId };
            var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l1); g.AddNode(l2); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l1", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [UnityTest]
        public IEnumerator AutoAdvance_MovesToNextLine_AfterDelay()
        {
            var graph = BuildTwoLineGraph();
            var view = new FakeView();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = view;
            driver.Provider = new CsvLocalizationProvider("Key,en\nline_l1,One\nline_l2,Two\n", "en");
            driver.ConfigureFlowForTest(auto: true, delay: 0.3f, timeout: 0f);
            try
            {
                driver.StartDialogue(graph);
                Assert.AreEqual("One", view.LastLine.ResolvedText);

                yield return new WaitForSeconds(0.5f); // driver.Update should auto-advance once

                Assert.AreEqual("Two", view.LastLine.ResolvedText, "Auto-advance should reach the second line.");
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }
    }
}
