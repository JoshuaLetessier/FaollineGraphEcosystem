using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for BaseRunner on a simple linear graph (Start → Statement → End).
    /// No SubGraph or history features tested here.
    /// </summary>
    public class BaseRunnerLinearTests
    {
        // ── Test fixtures ──────────────────────────────────────────────────────

        private BaseGraph _graph;
        private BaseContext _ctx;
        private NodeExecutorRegistry _registry;
        private BaseRunner _runner;

        private StartNodeData _startNode;
        private StatementNodeData _statementNode;
        private EndNodeData _endNode;

        private readonly List<UnityEngine.Object> _soInstances = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _graph    = ScriptableObject.CreateInstance<BaseGraph>();
            _ctx      = new BaseContext();
            _registry = new NodeExecutorRegistry();
            _runner   = new BaseRunner();

            _startNode = new StartNodeData
            {
                Id       = "start",
                NodeType = StartNodeData.NodeTypeId
            };
            _statementNode = new StatementNodeData
            {
                Id       = "stmt",
                NodeType = StatementNodeData.NodeTypeId
            };
            _endNode = new EndNodeData
            {
                Id        = "end",
                NodeType  = EndNodeData.NodeTypeId,
                EndReason = EndReason.Completed
            };

            _graph.AddNode(_startNode);
            _graph.AddNode(_statementNode);
            _graph.AddNode(_endNode);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "stmt" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "stmt",  ToNodeId = "end"  });
            _graph.EntryNodeId = "start";

            _soInstances.Add(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _soInstances) UnityEngine.Object.DestroyImmediate(so);
            _soInstances.Clear();
        }

        // ── Start ──────────────────────────────────────────────────────────────

        [Test]
        public void Start_TransitionsToNodeReady()
        {
            _runner.Start(_graph, _ctx, _registry);
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
        }

        [Test]
        public void Start_MissingEntryNodeId_ThrowsInvalidOperationException()
        {
            _graph.EntryNodeId = null;
            Assert.Throws<InvalidOperationException>(() => _runner.Start(_graph, _ctx, _registry));
        }

        [Test]
        public void Start_RaisesOnNodeEntered_ForEntryNode()
        {
            BaseNodeData entered = null;
            _runner.OnNodeEntered += n => entered = n;

            _runner.Start(_graph, _ctx, _registry);

            Assert.IsNotNull(entered);
            Assert.AreEqual("start", entered.Id);
        }

        [Test]
        public void Start_RaisesOnNodeCompleted_ForEntryNode()
        {
            BaseNodeData completed = null;
            _runner.OnNodeCompleted += n => completed = n;

            _runner.Start(_graph, _ctx, _registry);

            Assert.IsNotNull(completed);
            Assert.AreEqual("start", completed.Id);
        }

        // ── Proceed ────────────────────────────────────────────────────────────

        [Test]
        public void Proceed_AdvancesToNextNode()
        {
            var visited = new List<string>();
            _runner.OnNodeEntered += n => visited.Add(n.Id);

            _runner.Start(_graph, _ctx, _registry); // enters Start
            _runner.Proceed();                      // exits Start → enters Statement

            Assert.AreEqual(2, visited.Count);
            Assert.AreEqual("stmt", visited[1]);
        }

        [Test]
        public void Proceed_ExecutesFullNodeSequence()
        {
            var sequence = new List<string>();

            var enterAction = CreateTrackingAction("enterAction", sequence);
            _statementNode.OnEnterActions.Add(enterAction);

            var stmtExecutor = new LambdaExecutor(StatementNodeData.NodeTypeId,
                (n, _) => sequence.Add($"execute:{n.Id}"));
            _registry.Register(stmtExecutor);

            _runner.OnNodeCompleted += n =>
            {
                if (n.Id == "stmt") sequence.Add("completed:stmt");
            };

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed(); // enter Statement

            Assert.That(sequence, Is.EqualTo(new[] { "enterAction", "execute:stmt", "completed:stmt" }));
        }

        [Test]
        public void Proceed_OnExitActions_CalledOnCurrentNodeBeforeAdvancing()
        {
            var sequence = new List<string>();
            var exitAction = CreateTrackingAction("exitStart", sequence);
            _startNode.OnExitActions.Add(exitAction);

            _runner.OnNodeEntered += n =>
            {
                if (n.Id == "stmt") sequence.Add("entered:stmt");
            };

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(2, sequence.Count);
            Assert.AreEqual("exitStart",    sequence[0]);
            Assert.AreEqual("entered:stmt", sequence[1]);
        }

        [Test]
        public void Proceed_ReachesEndNode_TransitionsToEnded()
        {
            AutoProceed();
            Assert.AreEqual(RunnerState.Ended, _runner.State);
        }

        [Test]
        public void Proceed_AfterEnded_IsNoOp()
        {
            AutoProceed();
            Assert.DoesNotThrow(() => _runner.Proceed());
            Assert.AreEqual(RunnerState.Ended, _runner.State);
        }

        [Test]
        public void OnEnded_RaisedWithCorrectReason()
        {
            EndReason? received = null;
            _runner.OnEnded += r => received = r;

            AutoProceed();

            Assert.AreEqual(EndReason.Completed, received);
        }

        // ── EntryConditions ────────────────────────────────────────────────────

        [Test]
        public void EntryCondition_AllPass_NodeExecutes()
        {
            var executed = false;
            var cond = CreateCondition(true);
            _statementNode.EntryConditions.Add(cond);
            _registry.Register(new LambdaExecutor(StatementNodeData.NodeTypeId,
                (_, __) => executed = true));

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.IsTrue(executed);
        }

        [Test]
        public void EntryCondition_Fails_RaisesOnStuck()
        {
            bool stuck = false;
            _statementNode.EntryConditions.Add(CreateCondition(false));
            _runner.OnStuck += () => stuck = true;

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.IsTrue(stuck);
        }

        [Test]
        public void EntryCondition_Fails_RunnerStaysNodeReady()
        {
            _statementNode.EntryConditions.Add(CreateCondition(false));
            _runner.OnStuck += () => { };

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
        }

        // ── ChooseById ─────────────────────────────────────────────────────────

        [Test]
        public void ChooseById_SelectsEdgeByEdgeId()
        {
            BaseNodeData entered = null;
            _runner.OnNodeEntered += n => entered = n;

            _runner.Start(_graph, _ctx, _registry);
            _runner.ChooseById("e1");

            Assert.AreEqual("stmt", entered?.Id);
        }

        // ── Executor dispatch ──────────────────────────────────────────────────

        [Test]
        public void Execute_NoRegisteredExecutor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _runner.Start(_graph, _ctx, _registry));
        }

        [Test]
        public void Execute_RegisteredExecutor_IsCalled()
        {
            int calls = 0;
            _registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (_, __) => calls++));

            _runner.Start(_graph, _ctx, _registry);

            Assert.AreEqual(1, calls);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void AutoProceed()
        {
            _runner.OnNodeCompleted += _ =>
            {
                if (_runner.State == RunnerState.NodeReady) _runner.Proceed();
            };
            _runner.Start(_graph, _ctx, _registry);
        }

        private TrackingAction CreateTrackingAction(string label, List<string> log)
        {
            var a = ScriptableObject.CreateInstance<TrackingAction>();
            a.Label = label;
            a.Log   = log;
            _soInstances.Add(a);
            return a;
        }

        private ConstantCondition CreateCondition(bool value)
        {
            var c = ScriptableObject.CreateInstance<ConstantCondition>();
            c.Value = value;
            _soInstances.Add(c);
            return c;
        }

        // ── Mock helpers (ScriptableObject-safe) ──────────────────────────────

        private class TrackingAction : BaseAction
        {
            public string Label;
            public List<string> Log;
            public override void Execute(BaseContext context) => Log?.Add(Label);
        }

        private class ConstantCondition : BaseCondition
        {
            public bool Value;
            public override bool Evaluate(BaseContext context) => Value;
        }

        private class LambdaExecutor : INodeExecutor
        {
            private readonly Action<BaseNodeData, BaseContext> _exec;
            public string NodeType { get; }
            public LambdaExecutor(string type, Action<BaseNodeData, BaseContext> exec)
            { NodeType = type; _exec = exec; }
            public void Execute(BaseNodeData node, BaseContext context) => _exec(node, context);
        }
    }
}
