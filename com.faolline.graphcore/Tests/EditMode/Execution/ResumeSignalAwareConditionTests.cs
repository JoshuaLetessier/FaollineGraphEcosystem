using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// IResumeSignalAwareCondition + SignalPayloadMatchesCondition combined with a second OR'd awaited name
    /// (AwaitSignalName + AwaitSignalNamesExtra) — the case TODO.md's "Signal scoping" entry flagged as not
    /// yet supported. One condition per awaited name, each abstaining on the other's name, composes as the
    /// intended OR instead of an accidental AND. Also reproduces the original cross-talk repro end-to-end
    /// with two BaseRunner instances sharing one BaseContext (the actual proximity-streaming shape).
    /// </summary>
    public class ResumeSignalAwareConditionTests
    {
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();
        private NodeExecutorRegistry _registry;

        private SignalDef Sig(string name)
        {
            var s = SignalDef.Create(name);
            _so.Add(s);
            return s;
        }

        private SignalPayloadMatchesCondition Make(SignalDef signal, string expected, SignalPayloadMatchMode mode = SignalPayloadMatchMode.Exact)
        {
            var cond = ScriptableObject.CreateInstance<SignalPayloadMatchesCondition>();
            cond.Signal = signal;
            cond.ExpectedValue = expected;
            cond.MatchMode = mode;
            _so.Add(cond);
            return cond;
        }

        [SetUp]
        public void SetUp() => _registry = new NodeExecutorRegistry();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _so) UnityEngine.Object.DestroyImmediate(o);
            _so.Clear();
        }

        private (BaseGraph graph, BaseRunner runner, StatementNodeData room) BuildAwaitCompletedOrFailedGraph(
            SignalDef completed, SignalDef failed, string expectedValue)
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            _so.Add(graph);

            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var room  = new StatementNodeData
            {
                Id = "room", NodeType = StatementNodeData.NodeTypeId,
                AwaitSignalName = (string)completed
            };
            room.AwaitSignalNamesExtra.Add((string)failed);
            room.ResumeConditions.Add(Make(completed, expectedValue));
            room.ResumeConditions.Add(Make(failed, expectedValue, SignalPayloadMatchMode.StartsWith));

            var done = new StatementNodeData { Id = "done", NodeType = StatementNodeData.NodeTypeId };
            var end  = new EndNodeData       { Id = "end",  NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            graph.AddNode(start); graph.AddNode(room); graph.AddNode(done); graph.AddNode(end);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "room" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "room",  ToNodeId = "done" });
            graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "done",  ToNodeId = "end"  });
            graph.EntryNodeId = "start";

            var runner = new BaseRunner();
            return (graph, runner, room);
        }

        // ── One node, two OR'd names, one condition per name ───────────────────

        [Test]
        public void OwnCompletion_Resumes_WrongTilesCompletion_DoesNot()
        {
            var completed = Sig("loadCompleted");
            var failed    = Sig("loadFailed");
            var (graph, runner, _) = BuildAwaitCompletedOrFailedGraph(completed, failed, "ZoneSud");
            var ctx = new BaseContext();

            runner.Start(graph, ctx, _registry);
            runner.Proceed();
            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State);

            runner.RaiseSignal(completed, "ZoneNord");   // homonymous, wrong tile
            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State, "Must not resume on another tile's completion.");

            runner.RaiseSignal(completed, "ZoneSud");    // this tile's own completion
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
            Assert.AreEqual("done", runner.CurrentNode.Id);
        }

        [Test]
        public void OwnFailure_Resumes_WrongTilesFailure_DoesNot()
        {
            var completed = Sig("loadCompleted");
            var failed    = Sig("loadFailed");
            var (graph, runner, _) = BuildAwaitCompletedOrFailedGraph(completed, failed, "ZoneSud");
            var ctx = new BaseContext();

            runner.Start(graph, ctx, _registry);
            runner.Proceed();

            runner.RaiseSignal(failed, "ZoneNord: Scene 'ZoneNord' is not loaded.");
            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State, "Must not resume on another tile's failure.");

            runner.RaiseSignal(failed, "ZoneSud: Scene 'ZoneSud' is not loaded.");
            Assert.AreEqual(RunnerState.NodeReady, runner.State, "Own failure (payload prefix match) should resume.");
            Assert.AreEqual("done", runner.CurrentNode.Id);
        }

        // ── The original repro, end-to-end: two runners, one shared context ────

        [Test]
        public void SharedContext_TwoParkedRunners_OnlyMatchingOneResumesOnCompletion()
        {
            var loadCompleted = Sig("loadCompleted");
            var loadFailed    = Sig("loadFailed");
            var sharedCtx = new BaseContext();

            var (graphNord, runnerNord, _) = BuildAwaitCompletedOrFailedGraph(loadCompleted, loadFailed, "ZoneNord");
            var (graphSud,  runnerSud,  _) = BuildAwaitCompletedOrFailedGraph(loadCompleted, loadFailed, "ZoneSud");

            runnerNord.Start(graphNord, sharedCtx, _registry);
            runnerNord.Proceed();
            runnerSud.Start(graphSud, sharedCtx, _registry);
            runnerSud.Proceed();
            Assert.AreEqual(RunnerState.WaitingForSignal, runnerNord.State);
            Assert.AreEqual(RunnerState.WaitingForSignal, runnerSud.State);

            // One shared AsyncSceneLoader-style completion: ZoneNord's load lands first.
            runnerNord.RaiseSignal(loadCompleted, "ZoneNord");

            Assert.AreEqual(RunnerState.NodeReady, runnerNord.State, "ZoneNord should resume on its own completion.");
            Assert.AreEqual(RunnerState.WaitingForSignal, runnerSud.State,
                "ZoneSud must stay parked — without SignalPayloadMatchesCondition this used to false-resume here.");

            runnerSud.RaiseSignal(loadCompleted, "ZoneSud");
            Assert.AreEqual(RunnerState.NodeReady, runnerSud.State, "ZoneSud resumes once its own completion lands.");
        }
    }
}
