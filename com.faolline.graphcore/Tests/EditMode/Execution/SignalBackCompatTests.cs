using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// The non-breakage gate: with no awaiting nodes and no signals, behaviour is identical to 0.3.0;
    /// signals never enter the parameter snapshot, deep clone, or carry subscribers; an empty
    /// AwaitSignalName never holds.
    /// </summary>
    public class SignalBackCompatTests
    {
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _so) UnityEngine.Object.DestroyImmediate(so);
            _so.Clear();
        }

        [Test]
        public void NoAwait_NoSignal_RunsIdenticallyToBefore()
        {
            var graph    = ScriptableObject.CreateInstance<BaseGraph>();
            _so.Add(graph);
            var ctx      = new BaseContext();
            var registry = new NodeExecutorRegistry();
            var runner   = new BaseRunner();

            var start = new StartNodeData     { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var stmt  = new StatementNodeData { Id = "m", NodeType = StatementNodeData.NodeTypeId };
            var end   = new EndNodeData       { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            graph.AddNode(start);
            graph.AddNode(stmt);
            graph.AddNode(end);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "m" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "m", ToNodeId = "e" });
            graph.EntryNodeId = "s";

            EndReason? ended = null;
            runner.OnEnded += r => ended = r;

            runner.Start(graph, ctx, registry);
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
            runner.Proceed();                          // s → m
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
            runner.Proceed();                          // m → e (NodeReady)
            runner.Proceed();                          // e → Ended

            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.AreEqual(EndReason.Completed, ended);
        }

        [Test]
        public void Signals_ExcludedFrom_GetAllParameters_AndDeepClone()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 5);
            ctx.RaiseSignal<int>("evt", 99);

            var all = ctx.GetAllVariables();
            Assert.IsTrue(all.ContainsKey("Gold"));
            Assert.IsFalse(all.ContainsKey("evt"), "Signals must never enter the parameter snapshot.");

            var clone = ctx.DeepClone();
            Assert.AreEqual(5, clone.Get<int>("Gold"));
            Assert.IsFalse(clone.TryGetLastSignal("evt", out _), "DeepClone must not copy transient signals.");
        }

        [Test]
        public void DeepClone_DoesNotCopy_SignalSubscribers()
        {
            var ctx = new BaseContext();
            int hits = 0;
            ctx.OnSignal("evt", _ => hits++);

            var clone = ctx.DeepClone();
            clone.RaiseSignal("evt");

            Assert.AreEqual(0, hits, "Subscribers must not be carried into a clone.");
        }

        [Test]
        public void EmptyAwaitSignalName_IsDefault_AndDoesNotHold()
        {
            var node = new StatementNodeData { Id = "n", NodeType = StatementNodeData.NodeTypeId };
            Assert.AreEqual(string.Empty, node.AwaitSignalName);

            node.AwaitSignalName = null;
            Assert.AreEqual(string.Empty, node.AwaitSignalName, "Setter coerces null to empty.");
        }
    }
}
