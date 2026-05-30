using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Verifies that null condition/action entries in node lists are skipped with a warning
    /// rather than throwing NullReferenceException.
    /// </summary>
    [TestFixture]
    public class BaseRunnerNullSafetyTests
    {
        private class NullConditionNodeData : BaseNodeData
        {
            public NullConditionNodeData()
            {
                // Simulate a deleted ScriptableObject asset: null entry in EntryConditions
                EntryConditions.Add(null);
            }
        }

        private class NullActionNodeData : BaseNodeData
        {
            public NullActionNodeData()
            {
                OnEnterActions.Add(null);
                OnExitActions.Add(null);
            }
        }

        [Test]
        public void EntryCondition_NullEntry_IsSkippedWithWarning()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new NullConditionNodeData { Id = "s", NodeType = "test/null-cond" };
            var end   = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.AddNode(end);
            graph.EntryNodeId = "s";
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });

            var runner = new BaseRunner();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[GraphCore\].*[Nn]ull.*condition"));

            Assert.DoesNotThrow(() => runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()),
                "Runner must not throw when EntryConditions contains a null entry");

            Object.DestroyImmediate(graph);
        }

        [Test]
        public void OnEnterAction_NullEntry_IsSkippedWithWarning()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new NullActionNodeData { Id = "s", NodeType = "test/null-action" };
            var end   = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.AddNode(end);
            graph.EntryNodeId = "s";
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });

            var runner = new BaseRunner();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[GraphCore\].*[Nn]ull.*action"));

            Assert.DoesNotThrow(() =>
            {
                runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
                runner.Proceed();
            }, "Runner must not throw when OnEnterActions or OnExitActions contains a null entry");

            Object.DestroyImmediate(graph);
        }
    }
}
