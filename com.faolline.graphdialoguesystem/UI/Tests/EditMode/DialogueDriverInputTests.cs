using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>EditMode tests for the keyboard→action mapping seam (ChooseByIndex) on DialogueDriver.</summary>
    public class DialogueDriverInputTests
    {
        private const string Csv =
            "Key,en\n" +
            "line_l,Hello\n" +
            "line_la,AfterA\n" +
            "line_lb,AfterB\n";

        private AlwaysFalseCondition _gate;

        // Start → Line "l" → Choice [a → "la" → End, b (gated false) → "lb" → End]
        private DialogueGraph BuildGraph()
        {
            _gate = ScriptableObject.CreateInstance<AlwaysFalseCondition>();
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s  = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l  = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var c  = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a", Title = "A" });
            c.Choices.Add(new DialogueChoice { Id = "b", Title = "B", Condition = _gate });
            var la = new DialogueLineNodeData { Id = "la", NodeType = DialogueLineNodeData.NodeTypeId };
            var lb = new DialogueLineNodeData { Id = "lb", NodeType = DialogueLineNodeData.NodeTypeId };
            var ea = new EndNodeData { Id = "ea", NodeType = EndNodeData.NodeTypeId };
            var eb = new EndNodeData { Id = "eb", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(c); g.AddNode(la); g.AddNode(lb); g.AddNode(ea); g.AddNode(eb);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l",  PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l",  ToNodeId = "c",  PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "c",  ToNodeId = "la", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "c",  ToNodeId = "lb", PortName = "b" });
            g.AddEdge(new BaseEdgeData { Id = "e5", FromNodeId = "la", ToNodeId = "ea", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e6", FromNodeId = "lb", ToNodeId = "eb", PortName = "out" });
            return g;
        }

        private DialogueDriver NewDriver(out RecordingDialogueView view, out DialogueGraph graph)
        {
            graph = BuildGraph();
            view = new RecordingDialogueView();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = view;
            driver.Provider = new CsvLocalizationProvider(Csv, "en");
            return driver;
        }

        private void Cleanup(DialogueDriver driver, DialogueGraph graph)
        {
            if (driver != null) Object.DestroyImmediate(driver.gameObject);
            if (graph != null) Object.DestroyImmediate(graph);
            if (_gate != null) Object.DestroyImmediate(_gate);
        }

        [Test]
        public void ChooseByIndex_SelectsAvailableOption()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();             // → choices
                driver.ChooseByIndex(1);      // option A (available)
                Assert.IsNotNull(view.LastLine);
                Assert.AreEqual("AfterA", view.LastLine.ResolvedText);
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void ChooseByIndex_UnavailableOption_IsNoOp()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();             // → choices
                driver.ChooseByIndex(2);      // option B (gated false)
                Assert.IsNull(view.LastLine, "Unavailable option must not advance.");
                Assert.IsNotNull(view.LastChoices, "Still showing choices.");
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void ChooseByIndex_OutOfRange_IsNoOp()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();
                Assert.DoesNotThrow(() => driver.ChooseByIndex(9));
                Assert.IsNull(view.LastLine);
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void ChooseByIndex_DuringLine_IsNoOp()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);  // on line l (not choices)
                driver.ChooseByIndex(1);
                Assert.AreEqual("Hello", view.LastLine.ResolvedText, "Still on the line; no choice taken.");
            }
            finally { Cleanup(driver, graph); }
        }
    }
}
