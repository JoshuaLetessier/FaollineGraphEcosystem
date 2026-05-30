using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Headless end-to-end checks of the generated 008 sample (SampleAuthoringGraph): SubGraph descent,
    /// typed-condition choice filtering, and per-branch EndReason. Skips if the sample asset is absent
    /// (generate it via "Faolline/Create Sample TestGraph 008 (Authoring)").
    /// </summary>
    [TestFixture]
    public class SampleAuthoringGraphIntegrationTests
    {
        private const string Path =
            "Assets/FaollineGraphEcosystem/com.faolline.graphTest/Samples/SampleAuthoringGraph.asset";

        private static TestGraph LoadOrIgnore()
        {
            var graph = AssetDatabase.LoadAssetAtPath<TestGraph>(Path);
            if (graph == null)
                Assert.Ignore("SampleAuthoringGraph asset not present — run 'Faolline/Create Sample TestGraph 008 (Authoring)'.");
            return graph;
        }

        private static (BaseRunner runner, TestGameContext ctx, List<string> visited) StartSample(TestGraph graph)
        {
            var ctx = new TestGameContext();
            ctx.InitFromGraph(graph);
            var visited = new List<string>();
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => visited.Add(n.NodeType);
            runner.Start(graph, ctx, new NodeExecutorRegistry());
            return (runner, ctx, visited);
        }

        private static ChoiceNodeData DrainToChoice(BaseRunner runner)
        {
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 200)
            {
                if (runner.CurrentNode is ChoiceNodeData choice) return choice;
                runner.Proceed();
            }
            return null;
        }

        [Test]
        public void Sample_DescendsSubGraph_AndFiltersChoicesByTypedConditions()
        {
            var graph = LoadOrIgnore();
            var (runner, ctx, visited) = StartSample(graph);

            var choice = DrainToChoice(runner);

            Assert.IsNotNull(choice, "Execution must reach the Choice node");
            Assert.Contains(SubGraphNodeData.NodeTypeId, visited,
                "The SubGraph node must have been entered (sub-graph descent)");

            var available = new List<BaseChoice>();
            foreach (var c in choice.Choices)
                if (c.Condition == null || c.Condition.Evaluate(ctx))
                    available.Add(c);

            Assert.AreEqual(2, available.Count,
                "Win (score>=3) and Retreat (hp<0.5) pass; Surrender (name==villain) is filtered out");
        }

        [Test]
        public void Sample_ChooseWin_EndsCompleted()
        {
            var graph = LoadOrIgnore();
            var (runner, _, _) = StartSample(graph);
            EndReason reason = EndReason.Completed;
            runner.OnEnded += r => reason = r;

            var choice = DrainToChoice(runner);
            Assert.IsNotNull(choice);
            var win = choice.Choices.Find(c => (c as TestChoice)?.Label.StartsWith("Win") == true);
            Assert.IsNotNull(win, "Sample must have a Win choice");

            runner.ChooseById(win.Id);
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 200) runner.Proceed();

            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.AreEqual(EndReason.Completed, reason, "The Win branch must end with EndReason.Completed");
        }

        [Test]
        public void Sample_ChooseRetreat_EndsCancelled()
        {
            var graph = LoadOrIgnore();
            var (runner, _, _) = StartSample(graph);
            EndReason reason = EndReason.Completed;
            runner.OnEnded += r => reason = r;

            var choice = DrainToChoice(runner);
            Assert.IsNotNull(choice);
            var retreat = choice.Choices.Find(c => (c as TestChoice)?.Label.StartsWith("Retreat") == true);
            Assert.IsNotNull(retreat, "Sample must have a Retreat choice");

            runner.ChooseById(retreat.Id);
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 200) runner.Proceed();

            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.AreEqual(EndReason.Cancelled, reason, "The Retreat branch must end with EndReason.Cancelled");
        }
    }
}
