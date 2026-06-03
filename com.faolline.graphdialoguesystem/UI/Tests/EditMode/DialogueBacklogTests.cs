using NUnit.Framework;
using TMPro;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>The driver records a line history and a Canvas backlog mirrors it.</summary>
    public class DialogueBacklogTests
    {
        // Start → l1 → l2 → End
        private static DialogueGraph BuildGraph()
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

        [Test]
        public void History_And_Backlog_TrackShownLines()
        {
            var graph = BuildGraph();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = new RecordingDialogueView();
            driver.Provider = new CsvLocalizationProvider("Key,en\nline_l1,One\nline_l2,Two\n", "en");

            var backlogGo = new GameObject("backlog");
            var backlog = backlogGo.AddComponent<CanvasDialogueBacklog>();
            var contentGo = new GameObject("content", typeof(RectTransform));
            contentGo.transform.SetParent(backlogGo.transform);
            var templateGo = new GameObject("template", typeof(RectTransform));
            templateGo.transform.SetParent(backlogGo.transform);
            var template = templateGo.AddComponent<TextMeshProUGUI>();
            backlog.ConfigureForTest(driver, (RectTransform)contentGo.transform, template);

            try
            {
                driver.StartDialogue(graph);     // l1
                Assert.AreEqual(1, driver.History.Count);
                Assert.AreEqual(1, backlog.EntryCount);

                driver.Advance();                // l2
                Assert.AreEqual(2, driver.History.Count);
                Assert.AreEqual("One", driver.History[0].ResolvedText);
                Assert.AreEqual("Two", driver.History[1].ResolvedText);
                Assert.AreEqual(2, backlog.EntryCount);

                driver.StartDialogue(graph);     // restart clears history
                Assert.AreEqual(1, driver.History.Count);
            }
            finally
            {
                Object.DestroyImmediate(backlogGo);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(graph);
            }
        }
    }
}
