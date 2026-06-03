using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>The driver surfaces a stuck dialogue (no valid branch) via its OnStuck event + a warning.</summary>
    public class DialogueDriverStuckTests
    {
        private AlwaysFalseCondition _gate;

        // Start → Line "l" → (only edge to End gated false) → advancing the line gets stuck.
        private DialogueGraph BuildStuckGraph()
        {
            _gate = ScriptableObject.CreateInstance<AlwaysFalseCondition>();
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out", Condition = _gate });
            return g;
        }

        [Test]
        public void Advance_IntoDeadEnd_RaisesOnStuck()
        {
            var graph = BuildStuckGraph();
            var view = new RecordingDialogueView();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = view;
            driver.Provider = new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en");
            bool stuck = false;
            driver.OnStuck += () => stuck = true;
            try
            {
                driver.StartDialogue(graph);            // shows line l
                LogAssert.Expect(LogType.Warning, new Regex("stuck"));
                driver.Advance();                       // only edge gated false → stuck
                Assert.IsTrue(stuck, "Driver must raise OnStuck when no branch is available.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(graph);
                if (_gate != null) Object.DestroyImmediate(_gate);
            }
        }
    }
}
