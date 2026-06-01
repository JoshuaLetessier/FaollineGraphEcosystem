using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the dialogue system. Validates behaviour that requires a real Unity
    /// runtime: PlayerPrefs persistence, Resources.Load, and multi-frame coroutine playback.
    /// The headless logic (runner, conditions, choices, localization) is covered in EditMode.
    /// </summary>
    public class DialoguePlayerPlayModeTests
    {
        private const string SaveKey = "test_dialogue_save";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            LocalizationContext.Current = null;
        }

        // ── PlayerPrefs save/restore roundtrip ───────────────────────────────────

        /// <summary>
        /// Verifies that a DialogueSessionState can be saved to PlayerPrefs and restored on the
        /// next "session" — simulating a real game save between play sessions.
        /// </summary>
        [UnityTest]
        public IEnumerator SessionState_PersistsViaPlayerPrefs_AndRestoresCorrectly()
        {
            var graph = BuildLinearGraph(out var a1, out var a2);
            try
            {
                var provider = new CsvLocalizationProvider(string.Empty, "en");

                // ── Session 1: play to checkpoint and save ─────────────────────────
                var ctx1 = new DialogueContext();
                var player1 = new DialoguePlayer(graph, ctx1, provider);

                LineStep saved = null;
                player1.OnLine += s => { if (s.NodeId == "l1") saved = s; };
                player1.Start();    // paused at l1 (checkpoint), counter=1

                Assert.AreEqual(1, ctx1.Counter, "Enter action must have fired.");
                Assert.IsNotNull(saved);

                var json = player1.SaveState().ToJson();
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();

                yield return null; // simulate end of frame

                // ── Session 2: load from PlayerPrefs and resume ────────────────────
                var loadedJson = PlayerPrefs.GetString(SaveKey, null);
                Assert.IsNotNull(loadedJson, "Save must persist in PlayerPrefs.");

                var state = DialogueSessionState.FromJson(loadedJson);
                Assert.IsNotNull(state);
                Assert.AreEqual("l1", state.NodeId, "Saved node must be the checkpoint.");

                var ctx2 = new DialogueContext();
                var player2 = new DialoguePlayer(graph, ctx2, provider);
                player2.RestoreFrom(state);

                // l1's enter-action re-fires (checkpoint is re-entered)
                Assert.AreEqual(1, ctx2.Counter, "Restored context must reflect checkpoint state.");

                yield return null;

                LineStep l2Step = null;
                player2.OnLine += s => { if (s.NodeId == "l2") l2Step = s; };
                player2.Advance(); // l1 → l2, counter=2
                Assert.AreEqual(2, ctx2.Counter);
                Assert.IsNotNull(l2Step, "Playback must continue past the checkpoint.");
            }
            finally
            {
                Object.DestroyImmediate(a1);
                Object.DestroyImmediate(a2);
                Object.DestroyImmediate(graph);
            }
        }

        // ── LocalizationContext at runtime ────────────────────────────────────────

        /// <summary>
        /// Verifies that LocalizationContext.Current never throws at runtime and always returns
        /// a usable provider, even without a settings asset configured.
        /// </summary>
        [UnityTest]
        public IEnumerator LocalizationContext_NeverNullAtRuntime()
        {
            LocalizationContext.Current = null; // force re-init

            yield return null;

            var settings = LocalizationContext.Current;
            Assert.IsNotNull(settings, "LocalizationContext.Current must never be null.");
            Assert.IsNotNull(settings.Provider, "Provider must never be null.");

            // Resolution must return the fallback rather than throw.
            var result = settings.Resolve("some_key");
            Assert.IsFalse(string.IsNullOrEmpty(result), "Resolution must return a non-empty fallback.");
        }

        // ── Dialogue completes within expected frames ─────────────────────────────

        /// <summary>
        /// Runs a short dialogue to completion over multiple frames, verifying that
        /// events fire correctly in a real runtime environment.
        /// </summary>
        [UnityTest]
        public IEnumerator DialoguePlayer_CompletesLinearDialogue_OverFrames()
        {
            var graph = BuildLinearGraph(out var a1, out var a2);
            try
            {
                var provider = new CsvLocalizationProvider(string.Empty, "en");
                var ctx = new DialogueContext();
                var player = new DialoguePlayer(graph, ctx, provider);

                int lineCount = 0;
                EndStep endStep = null;
                player.OnLine += _ => lineCount++;
                player.OnEnded += s => endStep = s;

                player.Start();         // l1
                yield return null;

                player.Advance();       // l1 → l2
                yield return null;

                player.Advance();       // l2 → end
                yield return null;

                Assert.AreEqual(2, lineCount, "Two line events must fire.");
                Assert.IsNotNull(endStep, "Dialogue must reach EndStep.");
                Assert.AreEqual(EndReason.Completed, endStep.EndReason);
            }
            finally
            {
                Object.DestroyImmediate(a1);
                Object.DestroyImmediate(a2);
                Object.DestroyImmediate(graph);
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        /// <summary>Builds: Start → L1(checkpoint, counter=1) → L2(counter=2) → End.</summary>
        private static DialogueGraph BuildLinearGraph(out SetIntAction a1, out SetIntAction a2)
        {
            a1 = ScriptableObject.CreateInstance<SetIntAction>();
            a1.ParameterKey = DialogueContextKeys.Counter; a1.Value = 1;
            a2 = ScriptableObject.CreateInstance<SetIntAction>();
            a2.ParameterKey = DialogueContextKeys.Counter; a2.Value = 2;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var s  = new StartNodeData    { Id = "s",  NodeType = StartNodeData.NodeTypeId };
            var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId };
            l1.IsCheckpoint = true;
            l1.OnEnterActions.Add(a1);
            var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId };
            l2.OnEnterActions.Add(a2);
            var e  = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            graph.AddNode(s); graph.AddNode(l1); graph.AddNode(l2); graph.AddNode(e);
            graph.EntryNodeId = "s";
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l1", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e",  PortName = "out" });
            return graph;
        }
    }
}
