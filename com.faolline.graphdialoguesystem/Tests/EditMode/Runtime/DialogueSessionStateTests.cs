using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>P3 — dialogue session save/restore: JSON roundtrip and playback continuity.</summary>
    public class DialogueSessionStateTests
    {
        // ── JSON roundtrip ────────────────────────────────────────────────────────

        [Test]
        public void ToJson_FromJson_RoundTrip_PreservesAllFields()
        {
            var state = new DialogueSessionState
            {
                GraphGuid = "test-guid",
                NodeId    = "node-123",
                SavedAt   = "2026-06-01T00:00:00Z",
                ContextValues = new System.Collections.Generic.List<DialogueSessionState.ContextEntry>
                {
                    new DialogueSessionState.ContextEntry { Key = "Flag",    Type = "bool",   Value = "true" },
                    new DialogueSessionState.ContextEntry { Key = "Counter", Type = "int",    Value = "7" },
                    new DialogueSessionState.ContextEntry { Key = "Speed",   Type = "float",  Value = "1.5" },
                    new DialogueSessionState.ContextEntry { Key = "Name",    Type = "string", Value = "hero" },
                }
            };

            var json    = state.ToJson();
            var loaded  = DialogueSessionState.FromJson(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("test-guid", loaded.GraphGuid);
            Assert.AreEqual("node-123",  loaded.NodeId);
            Assert.AreEqual(4, loaded.ContextValues.Count);
        }

        [Test]
        public void FromJson_ReturnsNull_ForNullOrEmpty()
        {
            Assert.IsNull(DialogueSessionState.FromJson(null));
            Assert.IsNull(DialogueSessionState.FromJson(string.Empty));
        }

        // ── Capture ───────────────────────────────────────────────────────────────

        [Test]
        public void Capture_SerializesAllContextTypes()
        {
            var ctx = new BaseContext();
            ctx.Set("flag", true);
            ctx.Set("n", 42);
            ctx.Set("f", 3.14f);
            ctx.Set("s", "hello");

            var state = DialogueSessionState.Capture("g1", "node-1", ctx);

            Assert.AreEqual("g1",     state.GraphGuid);
            Assert.AreEqual("node-1", state.NodeId);
            Assert.AreEqual(4, state.ContextValues.Count);
        }

        // ── ApplyContext ──────────────────────────────────────────────────────────

        [Test]
        public void ApplyContext_RestoresAllTypes()
        {
            var original = new BaseContext();
            original.Set("flag",    true);
            original.Set("counter", 7);
            original.Set("speed",   2.5f);
            original.Set("name",    "archer");

            var state = DialogueSessionState.Capture("g", "n", original);

            var restored = new BaseContext();
            state.ApplyContext(restored);

            Assert.AreEqual(true,     restored.Get<bool>("flag"));
            Assert.AreEqual(7,        restored.Get<int>("counter"));
            Assert.AreEqual(2.5f,     restored.Get<float>("speed"), 0.0001f);
            Assert.AreEqual("archer", restored.Get<string>("name"));
        }

        [Test]
        public void ApplyContext_IsNullSafe()
        {
            var state = new DialogueSessionState();
            Assert.DoesNotThrow(() => state.ApplyContext(null));
        }

        // ── Save/Restore via DialoguePlayer ──────────────────────────────────────

        [Test]
        public void SaveAndRestore_ResumesFromSameNode_WithRestoredContext()
        {
            // Build: Start → L1(checkpoint, set counter=1) → L2(set counter=2) → End
            var a1 = ScriptableObject.CreateInstance<SetIntAction>();
            a1.ParameterKey = DialogueContextKeys.Counter; a1.Value = 1;
            var a2 = ScriptableObject.CreateInstance<SetIntAction>();
            a2.ParameterKey = DialogueContextKeys.Counter; a2.Value = 2;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s  = new StartNodeData    { Id = "s",  NodeType = StartNodeData.NodeTypeId };
                var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId };
                l1.IsCheckpoint = true;
                l1.OnEnterActions.Add(a1);
                var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId };
                l2.OnEnterActions.Add(a2);
                var e  = new EndNodeData      { Id = "e",  NodeType = EndNodeData.NodeTypeId };

                graph.AddNode(s); graph.AddNode(l1); graph.AddNode(l2); graph.AddNode(e);
                graph.EntryNodeId = "s";
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l1", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e",  PortName = "out" });

                var provider = new CsvLocalizationProvider(string.Empty, "en");
                var ctx1 = new DialogueContext();

                // ── Original session: play to l1 and save ─────────────────────────
                var player1 = new DialoguePlayer(graph, ctx1, provider);
                player1.Start();     // paused at l1, counter=1
                Assert.AreEqual(1, ctx1.Counter);

                var json = player1.SaveState().ToJson();
                Assert.IsNotNull(json);

                // ── Restored session: resume from saved state ─────────────────────
                var state = DialogueSessionState.FromJson(json);
                var ctx2  = new DialogueContext();
                var player2 = new DialoguePlayer(graph, ctx2, provider);

                player2.RestoreFrom(state);
                // l1's enter-action fires again → counter=1 again
                Assert.AreEqual(1, ctx2.Counter, "Restored session re-enters the checkpoint node.");

                LineStep lastLine = null;
                player2.OnLine += s2 => lastLine = s2;
                player2.Advance();   // l1 → l2, counter=2
                Assert.AreEqual(2, ctx2.Counter);
                Assert.AreEqual("l2", lastLine?.NodeId);
            }
            finally
            {
                Object.DestroyImmediate(a1);
                Object.DestroyImmediate(a2);
                Object.DestroyImmediate(graph);
            }
        }
    }
}
