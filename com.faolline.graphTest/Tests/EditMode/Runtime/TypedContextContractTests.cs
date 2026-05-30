using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Proves the typed-context contract a downstream-starter lib will copy (Principle VI):
    /// typed values (int/float/string and a typed subclass) survive history snapshot/restore, the
    /// clone keeps the subclass type, and change notifications fire. No editor required.
    /// </summary>
    [TestFixture]
    public class TypedContextContractTests
    {
        [Test]
        public void GoBack_RestoresTypedContextValues_IntFloatString()
        {
            var setInt   = ScriptableObject.CreateInstance<TestSetIntAction>();    setInt.ParameterKey   = "score"; setInt.Value   = 99;
            var setFloat = ScriptableObject.CreateInstance<TestSetFloatAction>();  setFloat.ParameterKey = "hp";    setFloat.Value = 1.5f;
            var setStr   = ScriptableObject.CreateInstance<TestSetStringAction>(); setStr.ParameterKey   = "name";  setStr.Value   = "boss";
            var g = ScriptableObject.CreateInstance<TestGraph>();
            try
            {
                var start = new StartNodeData         { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var a     = new TestStatementNodeData { Id = "a", NodeType = TestStatementNodeData.NodeTypeId };
                a.OnEnterActions.Add(setInt); a.OnEnterActions.Add(setFloat); a.OnEnterActions.Add(setStr);
                var b   = new TestStatementNodeData { Id = "b",   NodeType = TestStatementNodeData.NodeTypeId };
                var end = new EndNodeData           { Id = "end", NodeType = EndNodeData.NodeTypeId };
                g.AddNode(start); g.AddNode(a); g.AddNode(b); g.AddNode(end); g.EntryNodeId = "s";
                g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "a",   PortName = "out" });
                g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "a", ToNodeId = "b",   PortName = "out" });
                g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "b", ToNodeId = "end", PortName = "out" });

                var ctx = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(g, ctx, new NodeExecutorRegistry());
                int guard = 0;
                while (runner.State == RunnerState.NodeReady && guard++ < 100) runner.Proceed();

                Assert.IsTrue(ctx.TryGet<int>("score", out var sc) && sc == 99);
                Assert.IsTrue(ctx.TryGet<float>("hp", out var hp)); Assert.AreEqual(1.5f, hp, 0.0001f);
                Assert.IsTrue(ctx.TryGet<string>("name", out var nm) && nm == "boss");

                // GoBack all the way to the Start snapshot (taken before A's actions ran)
                string last = null; guard = 0;
                while (guard++ < 100)
                {
                    runner.GoBack();
                    var id = runner.CurrentNode?.Id;
                    if (id == "s" || id == last) break;
                    last = id;
                }

                Assert.AreEqual("s", runner.CurrentNode?.Id);
                Assert.IsFalse(ctx.Has("score"), "GoBack to Start must revert the int value");
                Assert.IsFalse(ctx.Has("hp"),    "GoBack to Start must revert the float value");
                Assert.IsFalse(ctx.Has("name"),  "GoBack to Start must revert the string value");
            }
            finally
            {
                Object.DestroyImmediate(setInt);
                Object.DestroyImmediate(setFloat);
                Object.DestroyImmediate(setStr);
                Object.DestroyImmediate(g);
            }
        }

        [Test]
        public void TypedContextSubclass_DeepClone_ReturnsSubtypeWithValues()
        {
            var ctx = new TestGameContext { DoorOpen = true, FlagA = true };

            var clone = ctx.DeepClone();

            Assert.IsInstanceOf<TestGameContext>(clone,
                "CreateCloneInstance() must return the subclass — otherwise GoBack history restore breaks silently");
            var typed = (TestGameContext)clone;
            Assert.IsTrue(typed.DoorOpen);
            Assert.IsTrue(typed.FlagA);
            Assert.IsFalse(typed.HasItem);
        }

        [Test]
        public void Context_OnParameterChanged_FiresOnSet_AndStopsAfterOff()
        {
            var ctx = new BaseContext();
            int fired = 0; object got = null;
            void Handler(object v) { fired++; got = v; }

            ctx.OnParameterChanged("k", Handler);
            ctx.Set<int>("k", 42);

            Assert.AreEqual(1, fired);
            Assert.AreEqual(42, got);

            ctx.OffParameterChanged("k", Handler);
            ctx.Set<int>("k", 7);

            Assert.AreEqual(1, fired, "After OffParameterChanged the handler must not fire again");
        }
    }
}
