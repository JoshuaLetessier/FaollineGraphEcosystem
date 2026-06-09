using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// US2 — scene transition is a graphcore action (not a node type), attachable to any node's enter or
    /// exit list. Verified against a recording stub loader (no real scene activation).
    /// </summary>
    public class LoadSceneActionTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown() { foreach (var o in _so) if (o) Object.DestroyImmediate(o); _so.Clear(); }

        private LoadSceneAction NewAction(string scene, LoadSceneMode mode)
        {
            var a = ScriptableObject.CreateInstance<LoadSceneAction>();
            a.SceneName = scene; a.Mode = mode;
            _so.Add(a);
            return a;
        }

        [Test]
        public void LoadSceneAction_HasCreateAssetMenu_WithExpectedMenuName()
        {
            var attr = (CreateAssetMenuAttribute)System.Attribute.GetCustomAttribute(
                typeof(LoadSceneAction), typeof(CreateAssetMenuAttribute));
            Assert.IsNotNull(attr, "LoadSceneAction must be creatable from Assets > Create.");
            Assert.AreEqual("GraphGameFlow/Actions/Load Scene", attr.menuName);
        }

        [Test]
        public void Execute_RecordsConfiguredScene_Single()
        {
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var a = NewAction("Level_02", LoadSceneMode.Single);

            a.Execute(ctx);

            Assert.AreEqual(1, stub.Calls.Count);
            Assert.AreEqual(("Level_02", LoadSceneMode.Single), stub.Calls[0]);
        }

        [Test]
        public void Execute_Additive_RecordsAdditiveMode()
        {
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var a = NewAction("Overlay", LoadSceneMode.Additive);

            a.Execute(ctx);

            Assert.AreEqual(("Overlay", LoadSceneMode.Additive), stub.Calls[0]);
        }

        [Test]
        public void Execute_EmptyScene_LogsErrorNoThrowNoLoad()
        {
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var a = NewAction("", LoadSceneMode.Single);

            LogAssert.Expect(LogType.Error, "[GraphGameFlow] LoadSceneAction has an empty scene name; ignored.");
            Assert.DoesNotThrow(() => a.Execute(ctx));
            Assert.AreEqual(0, stub.Calls.Count);
        }

        [Test]
        public void Execute_NonGameFlowContext_FallsBackWithoutThrow()
        {
            // A bare BaseContext carries no loader; the action falls back to a default UnitySceneLoader,
            // which gracefully logs (scene not in build settings) instead of throwing.
            var a = NewAction("UnregisteredScene", LoadSceneMode.Single);

            LogAssert.Expect(LogType.Error,
                "[GraphGameFlow] Scene 'UnregisteredScene' cannot be loaded (not in Build Settings / Addressables); ignored.");
            Assert.DoesNotThrow(() => a.Execute(new BaseContext()));
        }

        [Test]
        public void Action_OnExitList_RunsWhenLeavingNode_ViaRunner()
        {
            // INV-5: the action is node-agnostic and runs from an EXIT list, driven by the real runner —
            // proving it is not tied to node entry or to a dedicated scene node type.
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            g.EntryNodeId = "a";
            var a = new StatementNodeData { Id = "a", NodeType = StatementNodeData.NodeTypeId };
            a.OnExitActions.Add(NewAction("OnExitScene", LoadSceneMode.Single));
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(a); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "a", ToNodeId = "end" });

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => runner.Proceed();
            runner.Start(g, ctx, new NodeExecutorRegistry());

            Assert.AreEqual("OnExitScene", stub.LastScene, "the action ran on leaving node 'a' (exit list).");
        }
    }
}
