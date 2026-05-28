using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseRunnerTests
    {
        private BaseGraph              _graph;
        private BaseContext            _ctx;
        private NodeExecutorRegistry   _registry;
        private BaseRunner             _runner;
        private StartNodeData          _startNode;
        private StatementNodeData      _statementNode;
        private EndNodeData            _endNode;
        private readonly List<UnityEngine.Object>  _soInstances = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _graph         = ScriptableObject.CreateInstance<BaseGraph>();
            _ctx           = new BaseContext();
            _registry      = new NodeExecutorRegistry();
            _runner        = new BaseRunner();

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
            foreach (var so in _soInstances)
                UnityEngine.Object.DestroyImmediate(so);
            _soInstances.Clear();
        }

        // ── Start ─────────────────────────────────────────────────────────────

        [Test]
        public void Start_ValidGraph_TransitionsToNodeReady()
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
        public void Start_ValidGraph_RaisesOnNodeEntered()
        {
            BaseNodeData entered = null;
            _runner.OnNodeEntered += n => entered = n;

            _runner.Start(_graph, _ctx, _registry);

            Assert.IsNotNull(entered);
            Assert.AreEqual("start", entered.Id);
        }

        [Test]
        public void Start_ValidGraph_RaisesOnNodeCompleted()
        {
            BaseNodeData completed = null;
            _runner.OnNodeCompleted += n => completed = n;

            _runner.Start(_graph, _ctx, _registry);

            Assert.IsNotNull(completed);
            Assert.AreEqual("start", completed.Id);
        }

        // ── Proceed ───────────────────────────────────────────────────────────

        [Test]
        public void Proceed_FromNodeReady_AdvancesToNextNode()
        {
            var visited = new List<string>();
            _runner.OnNodeEntered += n => visited.Add(n.Id);

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(2, visited.Count);
            Assert.AreEqual("stmt", visited[1]);
        }

        [Test]
        public void Proceed_ExecutesFullNodeLifecycleOrder()
        {
            var sequence     = new List<string>();
            var enterAction  = CreateTrackingAction("enter:stmt", sequence);
            _statementNode.OnEnterActions.Add(enterAction);

            _registry.Register(new LambdaExecutor(StatementNodeData.NodeTypeId,
                (n, _) => sequence.Add($"exec:{n.Id}")));

            _runner.OnNodeCompleted += n =>
            {
                if (n.Id == "stmt") sequence.Add("completed:stmt");
            };

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.That(sequence, Is.EqualTo(new[] { "enter:stmt", "exec:stmt", "completed:stmt" }));
        }

        [Test]
        public void Proceed_OnExitActions_RunBeforeNextNodeEntered()
        {
            var sequence   = new List<string>();
            var exitAction = CreateTrackingAction("exit:start", sequence);
            _startNode.OnExitActions.Add(exitAction);

            _runner.OnNodeEntered += n =>
            {
                if (n.Id == "stmt") sequence.Add("entered:stmt");
            };

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(2,              sequence.Count);
            Assert.AreEqual("exit:start",   sequence[0]);
            Assert.AreEqual("entered:stmt", sequence[1]);
        }

        [Test]
        public void Proceed_ReachesEndNode_TransitionsToEnded()
        {
            AutoProceedAll();

            Assert.AreEqual(RunnerState.Ended, _runner.State);
        }

        [Test]
        public void Proceed_WhenEnded_IsNoOp()
        {
            AutoProceedAll();

            Assert.DoesNotThrow(() => _runner.Proceed());
            Assert.AreEqual(RunnerState.Ended, _runner.State);
        }

        // ── OnEnded ───────────────────────────────────────────────────────────

        [Test]
        public void OnEnded_RaisedWithCorrectEndReason()
        {
            EndReason? received = null;
            _runner.OnEnded += r => received = r;

            AutoProceedAll();

            Assert.AreEqual(EndReason.Completed, received);
        }

        [Test]
        public void OnEnded_EndReasonCancelled_RaisedCorrectly()
        {
            _endNode.EndReason = EndReason.Cancelled;
            EndReason? received = null;
            _runner.OnEnded += r => received = r;

            AutoProceedAll();

            Assert.AreEqual(EndReason.Cancelled, received);
        }

        [Test]
        public void OnEnded_EndReasonError_RaisedCorrectly()
        {
            _endNode.EndReason = EndReason.Error;
            EndReason? received = null;
            _runner.OnEnded += r => received = r;

            AutoProceedAll();

            Assert.AreEqual(EndReason.Error, received);
        }

        // ── EntryCondition ────────────────────────────────────────────────────

        [Test]
        public void EntryCondition_AllPass_NodeExecutes()
        {
            bool executed = false;
            _statementNode.EntryConditions.Add(CreateCondition(true));
            _registry.Register(new LambdaExecutor(StatementNodeData.NodeTypeId, (_, __) => executed = true));

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

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
        }

        // ── ChooseById ────────────────────────────────────────────────────────

        [Test]
        public void ChooseById_SelectsEdgeByEdgeId()
        {
            BaseNodeData entered = null;
            _runner.OnNodeEntered += n => entered = n;

            _runner.Start(_graph, _ctx, _registry);
            _runner.ChooseById("e1");

            Assert.AreEqual("stmt", entered?.Id);
        }

        [Test]
        public void ChooseById_SelectsEdgeByPortName()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            _soInstances.Add(g);

            g.AddNode(new StartNodeData     { Id = "n0", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new StatementNodeData { Id = "n1", NodeType = StatementNodeData.NodeTypeId });
            g.AddNode(new EndNodeData       { Id = "n2", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "ex1", FromNodeId = "n0", ToNodeId = "n1", PortName = "main" });
            g.AddEdge(new BaseEdgeData { Id = "ex2", FromNodeId = "n1", ToNodeId = "n2" });
            g.EntryNodeId = "n0";

            BaseNodeData entered = null;
            var r = new BaseRunner();
            r.OnNodeEntered += n => entered = n;
            r.Start(g, new BaseContext(), new NodeExecutorRegistry());

            r.ChooseById("main");

            Assert.AreEqual("n1", entered?.Id);
        }

        [Test]
        public void ChooseById_NoMatchingEdge_RaisesOnStuck()
        {
            bool stuck = false;
            _runner.OnStuck += () => stuck = true;

            _runner.Start(_graph, _ctx, _registry);
            _runner.ChooseById("nonexistent-id");

            Assert.IsTrue(stuck);
        }

        // ── Execute ───────────────────────────────────────────────────────────

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

        [Test]
        public void Execute_RegisteredExecutor_CalledWithCorrectNodeAndContext()
        {
            BaseNodeData receivedNode = null;
            BaseContext  receivedCtx  = null;
            _registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { receivedNode = n; receivedCtx = c; }));

            _runner.Start(_graph, _ctx, _registry);

            Assert.AreSame(_startNode, receivedNode);
            Assert.AreSame(_ctx,       receivedCtx);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void AutoProceedAll()
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

        // ── Inner stubs ───────────────────────────────────────────────────────

        private class TrackingAction : BaseAction
        {
            public string       Label;
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
