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
    /// Unloading is the other half of an additive scene flow: like <see cref="LoadSceneAction"/> it is a
    /// graphcore action (not a node type), resolved through the context's loader when that loader implements
    /// <see cref="ISceneUnloader"/>. Verified against the recording stub (no real scene activation).
    /// </summary>
    public class UnloadSceneActionTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown() { foreach (var o in _so) if (o) Object.DestroyImmediate(o); _so.Clear(); }

        private UnloadSceneAction NewAction(string scene)
        {
            var a = ScriptableObject.CreateInstance<UnloadSceneAction>();
            a.SceneName = scene;
            _so.Add(a);
            return a;
        }

        /// <summary>An <see cref="ISceneLoader"/> WITHOUT unload support, for the fallback path.</summary>
        private sealed class LoadOnlyLoader : ISceneLoader
        {
            public void LoadScene(string sceneName, LoadSceneMode mode) { }
        }

        [Test]
        public void UnloadSceneAction_HasCreateAssetMenu_WithExpectedMenuName()
        {
            var attr = (CreateAssetMenuAttribute)System.Attribute.GetCustomAttribute(
                typeof(UnloadSceneAction), typeof(CreateAssetMenuAttribute));
            Assert.IsNotNull(attr, "UnloadSceneAction must be creatable from Assets > Create.");
            Assert.AreEqual("GraphGameFlow/Actions/Unload Scene", attr.menuName);
        }

        [Test]
        public void Execute_RecordsConfiguredScene()
        {
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var a = NewAction("Overlay");

            a.Execute(ctx);

            CollectionAssert.AreEqual(new[] { "Overlay" }, stub.Unloads);
            Assert.AreEqual(0, stub.Calls.Count, "an unload must not register as a load.");
        }

        [Test]
        public void Execute_EmptyScene_LogsErrorNoThrowNoUnload()
        {
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var a = NewAction("");

            LogAssert.Expect(LogType.Error, "[GraphGameFlow] UnloadSceneAction has an empty scene name; ignored.");
            Assert.DoesNotThrow(() => a.Execute(ctx));
            Assert.AreEqual(0, stub.Unloads.Count);
        }

        [Test]
        public void Execute_LoaderWithoutUnloadSupport_WarnsAndFallsBack()
        {
            // The context's loader only implements ISceneLoader: the action warns, then unloads through the
            // default UnitySceneLoader — which, with no such scene loaded in EditMode, gracefully errors.
            var ctx = new GameFlowContext { SceneLoader = new LoadOnlyLoader() };
            var a = NewAction("Overlay");

            LogAssert.Expect(LogType.Warning,
                "[GraphGameFlow] UnloadSceneAction: the context's scene loader (LoadOnlyLoader) " +
                "does not implement ISceneUnloader; falling back to the default UnitySceneLoader unload.");
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] Scene 'Overlay' is not loaded; unload ignored.");
            Assert.DoesNotThrow(() => a.Execute(ctx));
        }

        [Test]
        public void Execute_NonGameFlowContext_FallsBackWithoutThrow()
        {
            // A bare BaseContext carries no loader; the action falls back to a default UnitySceneLoader,
            // which gracefully logs (scene not loaded) instead of throwing.
            var a = NewAction("UnknownScene");

            LogAssert.Expect(LogType.Error, "[GraphGameFlow] Scene 'UnknownScene' is not loaded; unload ignored.");
            Assert.DoesNotThrow(() => a.Execute(new BaseContext()));
        }

        [Test]
        public void Action_OnExitList_RunsWhenLeavingNode_ViaRunner()
        {
            // The action is node-agnostic and runs from an EXIT list, driven by the real runner — proving it
            // is not tied to node entry or to a dedicated scene node type (parity with LoadSceneAction).
            var stub = new StubSceneLoader();
            var ctx = new GameFlowContext { SceneLoader = stub };
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            g.EntryNodeId = "a";
            var a = new StatementNodeData { Id = "a", NodeType = StatementNodeData.NodeTypeId };
            a.OnExitActions.Add(NewAction("OnExitOverlay"));
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(a); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "a", ToNodeId = "end" });

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => runner.Proceed();
            runner.Start(g, ctx, new NodeExecutorRegistry());

            CollectionAssert.AreEqual(new[] { "OnExitOverlay" }, stub.Unloads, "the action ran on leaving node 'a' (exit list).");
        }
    }
}
